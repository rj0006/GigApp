using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Bidding;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Banking;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Orders;
using GigApp.Api.Services.Ratings;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Tracking;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("customer")]
    public class CustomerController : PortalControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;
        private readonly IServiceItemLookup _serviceItems;
        private readonly IBidService _bids;

        public CustomerController(
            IAuthService authService,
            IProfileService profileService,
            IAddressService addressService,
            AppDbContext context,
            ICategoryLookup categories,
            IServiceItemLookup serviceItems,
            IBidService bids,
            IRatingService ratings,
            IBankAccountService bankAccounts,
            IOrderHistoryService orderHistory,
            ISupportService support)
            : base(authService, profileService, addressService, bankAccounts, orderHistory, support, ratings)
        {
            _context = context;
            _categories = categories;
            _serviceItems = serviceItems;
            _bids = bids;
        }

        private string TasksPath => $"{ProfilePath}/{ProfileSections.Tasks}";
        private string OrdersPath => $"{ProfilePath}/{ProfileSections.Orders}";

        protected override string PortalSlug => "customer";
        protected override string RequiredRole => UserRoles.Customer;

        [HttpGet("login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl, bool denied = false)
        {
            // Already signed in as a customer — no reason to show the form again.
            if (IsAlreadySignedIn) return RedirectToLocalOr(returnUrl);

            ViewData["Title"] = "Customer sign in";
            return View(BuildLoginModel(returnUrl, denied));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [SkipTracking]   // signing in changes no data
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["Title"] = "Customer sign in";
            if (!ModelState.IsValid) return View(model);

            var failed = await SignInAsync(
                () => AuthService.LoginAsync(new LoginRequest
                {
                    Identifier = model.Identifier,
                    Password = model.Password,
                    Role = RequiredRole,
                }),
                nameof(Login), model);

            return failed ?? RedirectToLocalOr(model.ReturnUrl);
        }

        [HttpGet("register")]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (IsAlreadySignedIn) return Redirect(DashboardPath);

            ViewData["Title"] = "Create a customer account";
            return View(new RegisterCustomerViewModel());
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterCustomerViewModel model)
        {
            ViewData["Title"] = "Create a customer account";
            if (!ModelState.IsValid) return View(model);

            var failed = await SignInAsync(
                () => AuthService.RegisterCustomerAsync(new RegisterCustomerRequest
                {
                    Name = model.Name,
                    Phone = model.Phone,
                    Email = model.Email,
                    Password = model.Password,
                }),
                nameof(Register), model);

            if (failed is not null) return failed;

            TempData["Success"] = "Welcome to GigApp. Your account is ready.";
            return Redirect(DashboardPath);
        }

        [HttpGet("")]
        [Authorize(Policy = Policies.CustomerOnly)]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewData["Title"] = "Services";

            var userId = User.GetRequiredUserId();

            var categories = await _context.SkillCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new CatalogCategoryViewModel
                {
                    Category = SkillCategoryDto.From(c),
                    ServiceCount = c.ServiceItems.Count(s => s.IsActive),
                    StartingFrom = c.ServiceItems
                        .Where(s => s.IsActive && s.AllowsInstantBooking && s.BasePayout > 0)
                        .Min(s => s.BasePayout),
                })
                .ToListAsync(ct);

            return View(new CustomerCatalogViewModel
            {
                Name = User.Identity?.Name ?? "there",
                Categories = categories,
                OpenTaskCount = await _context.GigTasks.CountAsync(
                    t => t.CustomerId == userId && GigTaskStatus.Open.Contains(t.Status), ct),
            });
        }

        protected override async Task<ProfileExtras> LoadProfileExtrasAsync(
            string section, CancellationToken ct)
        {
            if (section != ProfileSections.PostTask && section != ProfileSections.Tasks)
                return new ProfileExtras();

            return new ProfileExtras
            {
                Work = await BuildWorkAsync(User.GetRequiredUserId(), section, ct),
            };
        }

        private async Task<CustomerDashboardViewModel> BuildWorkAsync(
            int userId, string section, CancellationToken ct)
        {
            var tasks = await _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.ServiceItem)
                .Include(t => t.Partner)!.ThenInclude(p => p!.User)
                .Include(t => t.Partner)!.ThenInclude(p => p!.SkillCategory)
                .Where(t => t.CustomerId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            // Everyone the customer might click on: partners already assigned,
            // plus anyone who has bid on one of their open tasks.
            var taskIds = tasks.Select(t => t.Id).ToList();

            // Fetch the ids first, then load the partners. Include cannot be
            // applied after a Select that projects through a navigation.
            var bidderPartnerIds = await _context.TaskBids
                .AsNoTracking()
                .Where(b => taskIds.Contains(b.GigTaskId) && b.Status != BidStatus.Withdrawn)
                .Select(b => b.PartnerId)
                .Distinct()
                .ToListAsync(ct);

            var bidderPartners = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .Where(p => bidderPartnerIds.Contains(p.Id))
                .ToListAsync(ct);

            var partners = tasks
                .Where(t => t.Partner is not null)
                .Select(t => t.Partner!)
                .Concat(bidderPartners)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            var completedCounts = await _context.GigTasks
                .Where(t => t.Status == GigTaskStatus.Completed && t.PartnerId != null)
                .GroupBy(t => t.PartnerId!.Value)
                .Select(g => new { PartnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PartnerId, x => x.Count, ct);

            // Bids only matter while a task is still open for them.
            var openTaskIds = tasks
                .Where(t => t.Status == GigTaskStatus.Pending)
                .Select(t => t.Id)
                .ToList();

            var bidsByTask = new Dictionary<int, IReadOnlyList<BidDto>>();
            foreach (var taskId in openTaskIds)
                bidsByTask[taskId] = await _bids.ForTaskAsync(userId, taskId, ct);

            return new CustomerDashboardViewModel
            {
                Name = User.Identity?.Name ?? "there",
                Tasks = tasks.Select(GigTaskDto.From).ToList(),
                Categories = await _categories.GetActiveOptionsAsync(ct),
                Partners = partners
                    .Select(p => PartnerPublicDto.From(p, completedCounts.GetValueOrDefault(p.Id)))
                    .ToList(),
                BidsByTask = bidsByTask,
                Addresses = await AddressService.ListAsync(userId, ct),
                MyRatings = await Ratings.ForTasksAsync(taskIds, RatedBy.Customer, ct),
                PresetCategoryId = section == ProfileSections.PostTask
                    && int.TryParse(Request.Query["categoryId"], out var preset)
                        ? preset
                        : null,
            };
        }

        [HttpPost("tasks")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask(CreateGigTaskRequest newTask, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState
                    .SelectMany(e => e.Value!.Errors)
                    .Select(e => e.ErrorMessage));

                return Redirect(TasksPath);
            }

            if (!await _categories.IsSelectableAsync(newTask.CategoryId, ct))
            {
                TempData["Error"] = "Choose a valid category.";
                return Redirect(TasksPath);
            }

            if (!TaskUrgency.IsValid(newTask.Urgency))
            {
                TempData["Error"] = "Choose a valid urgency.";
                return Redirect(TasksPath);
            }

            var service = await _serviceItems.GetBookableAsync(
                newTask.ServiceItemId, newTask.CategoryId, ct);

            if (service is null)
            {
                TempData["Error"] = "Choose a service that belongs to the selected category.";
                return Redirect(TasksPath);
            }

            var customerId = User.GetRequiredUserId();

            // Ownership is checked here, not taken on trust from the form.
            var address = await AddressService.FindOwnedAsync(customerId, newTask.AddressId, ct);
            if (address is null)
            {
                TempData["Error"] = "Choose one of your saved addresses.";
                return Redirect(TasksPath);
            }

            var task = new GigTask
            {
                CustomerId = customerId,
                CategoryId = newTask.CategoryId,
                ServiceItemId = newTask.ServiceItemId,
                Urgency = newTask.Urgency,
                Description = newTask.Description.Trim(),

                // Snapshot the address so editing it later cannot relocate
                // work that has already happened.
                AddressId = address.Id,
                Address = address.ToSingleLine(),
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                Budget = service.IsInstant ? service.FixedPrice : newTask.Budget,
                AgreedAmount = service.IsInstant ? service.FixedPrice : null,
                BookingMode = service.IsInstant ? TaskBookingMode.Instant : TaskBookingMode.Bidding,
                PreferredDateTime = newTask.PreferredDateTime.ToUtc(),
                Status = GigTaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };

            _context.GigTasks.Add(task);
            await _context.SaveChangesAsync(ct);

            TrackDoc(task.Id, GigTaskDto.From(task));

            TempData["Success"] = service.IsInstant
                ? $"Booked at the fixed price of ₹{service.FixedPrice:N0}. The first available partner will take it, so there is nothing to compare."
                : "Your task has been posted. Partners can now bid on it.";

            return Redirect(TasksPath);
        }

        [HttpPost("tasks/{id:int}/rate")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("TaskRating")]
        public async Task<IActionResult> RateTask(
            int id, RateTaskRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Choose between one and five stars.";
                return Redirect(OrdersPath);
            }

            var result = await Ratings.RateAsync(
                User.GetRequiredUserId(), RatedBy.Customer, id, form.Stars, form.Feedback, ct);

            if (result.Succeeded)
                TempData["Success"] = "Thank you. Your rating helps other customers choose well.";
            else
                TempData["Error"] = result.Error;

            return Redirect(OrdersPath);
        }

        [HttpPost("tasks/{id:int}/cancel")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTask(int id, CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();
            var task = await _context.GigTasks.FirstOrDefaultAsync(t => t.Id == id && t.CustomerId == userId, ct);

            if (task is null)
            {
                TempData["Error"] = "Task not found.";
            }
            else if (!GigTaskStatus.CanTransition(task.Status, GigTaskStatus.Cancelled))
            {
                TempData["Error"] = $"A {task.Status.Replace('_', ' ')} task cannot be cancelled.";
            }
            else
            {
                task.Status = GigTaskStatus.Cancelled;
                await _context.SaveChangesAsync(ct);

                TrackDoc(task.Id, GigTaskDto.From(task));
                TempData["Success"] = $"Task #{id} cancelled.";
            }

            return Redirect(TasksPath);
        }

        [HttpPost("bids/{bidId:int}/accept")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Bid")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> AcceptBid(int bidId, CancellationToken ct)
        {
            var result = await _bids.AcceptAsync(User.GetRequiredUserId(), bidId, ct);
            if (!result.Succeeded) return BidError(result.Error);

            TrackDoc(bidId, result.Bid);
            TempData["Success"] =
                $"{result.Bid!.PartnerName} is assigned at ₹{result.Bid.Amount:N0}.";

            return Redirect(TasksPath);
        }

        [HttpPost("bids/{bidId:int}/counter")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Bid")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> CounterBid(
            int bidId, CounterBidRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BidError(FirstModelError());

            var result = await _bids.CounterAsync(User.GetRequiredUserId(), bidId, form, ct);
            if (!result.Succeeded) return BidError(result.Error);

            TrackDoc(bidId, result.Bid);
            TempData["Success"] =
                $"Counter offer of ₹{form.CounterAmount:N0} sent to {result.Bid!.PartnerName}.";

            return Redirect(TasksPath);
        }

        [HttpPost("bids/{bidId:int}/reject")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Bid")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> RejectBid(int bidId, CancellationToken ct)
        {
            var result = await _bids.RejectAsync(User.GetRequiredUserId(), bidId, ct);
            if (!result.Succeeded) return BidError(result.Error);

            TrackDoc(bidId, result.Bid);
            TempData["Success"] = "Bid rejected.";
            return Redirect(TasksPath);
        }

        private IActionResult BidError(string? message)
        {
            TempData["Error"] = message ?? "Could not update that bid.";
            return Redirect(TasksPath);
        }

        private string? FirstModelError() => ModelState
            .SelectMany(e => e.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

        [HttpPost("logout")]
        [SkipTracking]   // signing out changes no data
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            ClearAuthCookie();
            return Redirect(LoginPath);
        }
    }
}
