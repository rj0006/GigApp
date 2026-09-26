using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Earnings;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Geo;
using GigApp.Api.Services.Booking;
using GigApp.Api.Services.Notifications;
using GigApp.Api.Services.Ratings;
using GigApp.Api.Services.Realtime;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GigTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IServiceItemLookup _serviceItems;
        private readonly IAddressService _addresses;
        private readonly IEarningsService _earnings;
        private readonly ITaskClaimService _claims;
        private readonly IOfferService _offers;
        private readonly IRatingService _ratings;
        private readonly IMatchService _match;
        private readonly INotificationService _notifier;
        private readonly IRealtimeNotifier _realtime;
        private readonly ILogger<GigTasksController> _logger;

        public GigTasksController(
            AppDbContext context,
            IServiceItemLookup serviceItems,
            IAddressService addresses,
            IEarningsService earnings,
            ITaskClaimService claims,
            IOfferService offers,
            IRatingService ratings,
            IMatchService match,
            INotificationService notifier,
            IRealtimeNotifier realtime,
            ILogger<GigTasksController> logger)
        {
            _context = context;
            _serviceItems = serviceItems;
            _addresses = addresses;
            _earnings = earnings;
            _claims = claims;
            _offers = offers;
            _ratings = ratings;
            _match = match;
            _notifier = notifier;
            _realtime = realtime;
            _logger = logger;
        }

        // POST: api/gigtasks  -> Customer posts a new task
        [HttpPost]
        [Authorize(Policy = Policies.CustomerOnly)]
        public async Task<ActionResult<GigTaskDto>> CreateTask(
            CreateGigTaskRequest request, CancellationToken ct)
        {
            var categoryIsSelectable = await _context.SkillCategories
                .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct);

            if (!categoryIsSelectable)
                return BadRequest(new ProblemDetails { Title = "Choose a valid category.", Status = 400 });

            if (!TaskUrgency.IsValid(request.Urgency))
                return BadRequest(new ProblemDetails { Title = "Choose a valid urgency.", Status = 400 });

            var service = await _serviceItems.GetBookableAsync(
                request.ServiceItemId, request.CategoryId, ct);

            if (service is null)
                return BadRequest(new ProblemDetails
                {
                    Title = "Choose a service that belongs to the selected category.",
                    Status = 400,
                });

            var customerId = User.GetRequiredUserId();

            // Ownership is checked here, not taken on trust from the payload.
            var address = await _addresses.FindOwnedAsync(customerId, request.AddressId, ct);
            if (address is null)
                return BadRequest(new ProblemDetails { Title = "Choose one of your saved addresses.", Status = 400 });

            // CustomerId comes from the token, never the request body.
            var task = new GigTask
            {
                CustomerId = customerId,
                CategoryId = request.CategoryId,
                ServiceItemId = request.ServiceItemId,
                Urgency = request.Urgency,
                Description = request.Description.Trim(),

                // Snapshot the address. Editing or deleting it later must not
                // silently relocate work that has already happened.
                AddressId = address.Id,
                Address = address.ToSingleLine(),
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                Location = GeoPoint.From(address.Latitude, address.Longitude),
                Budget = service.IsInstant ? service.FixedPrice : request.Budget,
                AgreedAmount = service.IsInstant ? service.FixedPrice : null,
                BookingMode = service.IsInstant ? TaskBookingMode.Instant : TaskBookingMode.Bidding,
                PreferredDateTime = request.PreferredDateTime.ToUtc(),
                Status = GigTaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };

            _context.GigTasks.Add(task);
            await _context.SaveChangesAsync(ct);

            if (service.IsInstant) await _offers.StartAsync(task.Id, ct);

            // A bidding task appears on every matching partner's board the
            // moment it exists; an instant one already went into the offer
            // chain above, but the admin task list still wants to know either way.
            if (!service.IsInstant)
                await _realtime.NotifyCategoryPartnersAsync(task.CategoryId, "tasks", ct);
            await _realtime.NotifyAdminsAsync("tasks", ct);

            // Reload so the response carries the category and customer names,
            // matching what every other endpoint returns.
            return CreatedAtAction(
                nameof(GetTaskById),
                new { id = task.Id },
                GigTaskDto.From(await LoadDetailedAsync(task.Id, ct)));
        }

        // GET: api/gigtasks/available  -> Verified partners browse open work
        [HttpGet("available")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<IEnumerable<GigTaskDto>>> GetAvailableTasks(
            [FromQuery] int? categoryId, CancellationToken ct)
        {
            var partner = await GetCurrentPartnerAsync(ct);
            if (partner is null) return Forbid();

            if (!partner.IsVerified)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ProblemDetails { Title = "Your account is pending KYC verification.", Status = 403 });

            var query = _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Customer)
                .Include(t => t.Category)
                .Where(t => t.Status == GigTaskStatus.Pending);

            if (categoryId is not null)
                query = query.Where(t => t.CategoryId == categoryId);

            var tasks = await query
                .OrderBy(t => t.Urgency == TaskUrgency.Urgent ? 0
                            : t.Urgency == TaskUrgency.Normal ? 1 : 2)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            return Ok(tasks.Select(GigTaskDto.From));
        }

        // GET: api/gigtasks/my  -> Caller's own tasks, whichever side they are on
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<GigTaskDto>>> GetMyTasks(CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();

            var query = DetailedTasks;

            if (User.IsInRole(UserRoles.Partner))
            {
                var partner = await GetCurrentPartnerAsync(ct);
                if (partner is null) return Forbid();
                query = query.Where(t => t.PartnerId == partner.Id);
            }
            else
            {
                query = query.Where(t => t.CustomerId == userId);
            }

            var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
            return Ok(tasks.Select(GigTaskDto.From));
        }

        // GET: api/gigtasks/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<GigTaskDto>> GetTaskById(int id, CancellationToken ct)
        {
            var task = await DetailedTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

            if (task is null) return NotFound();

            if (!await CanViewAsync(task, ct)) return Forbid();

            return Ok(GigTaskDto.From(task));
        }

        // GET: api/gigtasks/customer/3  -> Admin lookup of any customer's history
        [HttpGet("customer/{customerId:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<IEnumerable<GigTaskDto>>> GetTasksByCustomer(
            int customerId, CancellationToken ct)
        {
            var tasks = await DetailedTasks
                .Where(t => t.CustomerId == customerId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            return Ok(tasks.Select(GigTaskDto.From));
        }

        // GET: api/gigtasks/all?page=1&pageSize=10&search=&status=&categoryId=  -> Admin list page
        [HttpGet("all")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<PagedResult<GigTaskDto>>> GetAllPaged(
            [FromQuery] PageRequest paging, string? status, int? categoryId, CancellationToken ct)
        {
            var query = DetailedTasks.Include(t => t.AssignedBy).AsQueryable();

            if (GigTaskStatus.IsValid(status)) query = query.Where(t => t.Status == status);
            if (categoryId is not null) query = query.Where(t => t.CategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(t =>
                    t.Description.ToLower().Contains(term) || t.Address.ToLower().Contains(term));
            }

            var page = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return Ok(page.Map(GigTaskDto.From));
        }

        // GET: api/gigtasks/5/matches  -> Ranked partners for the admin assign dialog
        [HttpGet("{id:int}/matches")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<IReadOnlyList<PartnerMatchDto>>> GetMatches(
            int id, CancellationToken ct) =>
            Ok(await _match.RankPartnersAsync(id, 8, ct));

        // POST: api/gigtasks/5/assign  -> Support assigns or reassigns a partner by hand
        [HttpPost("{id:int}/assign")]
        [Authorize(Policy = Policies.AdminOnly)]
        [TrackForm("TaskAssignment")]
        public async Task<ActionResult<GigTaskDto>> AssignTask(
            int id, AssignTaskRequest request, CancellationToken ct)
        {
            var result = await _claims.AssignAsync(
                User.GetRequiredUserId(), id, request.PartnerId, request.Note, ct);

            return result.Outcome switch
            {
                TaskClaimOutcome.Claimed => Ok(GigTaskDto.From(await LoadDetailedAsync(id, ct))),
                TaskClaimOutcome.NotFound => NotFound(),
                TaskClaimOutcome.Taken => Conflict(
                    new ProblemDetails { Title = result.Error, Status = 409 }),
                _ => BadRequest(new ProblemDetails { Title = result.Error, Status = 400 }),
            };
        }

        // PUT: api/gigtasks/5/accept  -> Partner claims a fixed-price task
        [HttpPut("{id:int}/accept")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<GigTaskDto>> AcceptTask(int id, CancellationToken ct)
        {
            var result = await _claims.ClaimAsync(User.GetRequiredUserId(), id, ct);

            return result.Outcome switch
            {
                TaskClaimOutcome.Claimed => Ok(GigTaskDto.From(await LoadDetailedAsync(id, ct))),
                TaskClaimOutcome.NotFound => NotFound(),
                TaskClaimOutcome.Taken => Conflict(
                    new ProblemDetails { Title = result.Error, Status = 409 }),
                _ => StatusCode(StatusCodes.Status403Forbidden,
                    new ProblemDetails { Title = result.Error, Status = 403 }),
            };
        }

        // PUT: api/gigtasks/5/status  -> Move a task along its lifecycle
        [HttpPut("{id:int}/status")]
        public async Task<ActionResult<GigTaskDto>> UpdateStatus(
            int id, UpdateGigTaskStatusRequest request, CancellationToken ct)
        {
            var newStatus = request.Status?.Trim().ToLowerInvariant() ?? string.Empty;

            if (!GigTaskStatus.IsValid(newStatus))
                return BadRequest(new ProblemDetails { Title = $"Unknown status '{request.Status}'.", Status = 400 });

            var task = await _context.GigTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (task is null) return NotFound();

            var permitted = await GetPermittedTransitionAsync(task, newStatus, ct);
            if (permitted is not null) return permitted;

            if (!GigTaskStatus.CanTransition(task.Status, newStatus))
                return Conflict(new ProblemDetails
                {
                    Title = $"Cannot move a task from '{task.Status}' to '{newStatus}'.",
                    Status = 409,
                });

            var isPartner = User.IsInRole(UserRoles.Partner);

            if (newStatus == GigTaskStatus.Cancelled && isPartner && task.PartnerId is not null)
            {
                await ReopenAfterCancellationAsync(task, task.PartnerId.Value, request.CancelReason, ct);
                return Ok(GigTaskDto.From(await LoadDetailedAsync(id, ct)));
            }

            var isCompleting = newStatus == GigTaskStatus.Completed;

            if (isCompleting && isPartner)
            {
                if (!RatingScale.IsValid(request.Stars))
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Rate the customer before you close this job.",
                        Status = 400,
                    });

                await _context.Entry(task).Reference(t => t.Partner).LoadAsync(ct);

                var rating = await _ratings.BuildAsync(
                    User.GetRequiredUserId(), RatedBy.Partner, task,
                    request.Stars, request.Feedback, ct);

                if (rating is not null) _context.TaskRatings.Add(rating);
            }

            task.Status = newStatus;
            task.CompletedAt = isCompleting ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync(ct);

            if (isCompleting)
            {
                await _earnings.PostJobEarningAsync(task, ct);
                if (isPartner) await _ratings.RefreshAverageAsync(task.CustomerId, ct);
            }

            _logger.LogInformation("Task {TaskId} moved to {Status}", id, newStatus);

            await _realtime.NotifyUserAsync(task.CustomerId, "tasks", ct);
            if (task.PartnerId is not null)
                await _realtime.NotifyUserAsync(await PartnerUserIdAsync(task.PartnerId.Value, ct), "tasks", ct);
            await _realtime.NotifyAdminsAsync("tasks", ct);

            return Ok(GigTaskDto.From(await LoadDetailedAsync(id, ct)));
        }

        private Task<int> PartnerUserIdAsync(int partnerId, CancellationToken ct) =>
            _context.Partners.Where(p => p.Id == partnerId).Select(p => p.UserId).FirstAsync(ct);

        private async Task ReopenAfterCancellationAsync(
            GigTask task, int partnerId, string? reason, CancellationToken ct)
        {
            _context.TaskCancellations.Add(new TaskCancellation
            {
                GigTaskId = task.Id,
                PartnerId = partnerId,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CancelledAt = DateTime.UtcNow,
            });

            var bookingMode = task.BookingMode;

            task.PartnerId = null;
            task.Status = GigTaskStatus.Pending;
            task.AssignedByUserId = null;
            task.AssignedAt = null;
            task.AssignmentNote = null;
            await _context.SaveChangesAsync(ct);

            var partnerName = await _context.Partners
                .Where(p => p.Id == partnerId)
                .Select(p => p.User!.Name)
                .FirstOrDefaultAsync(ct) ?? "The partner";

            await _notifier.PushAsync(new NotificationRequest(
                task.CustomerId,
                NotificationTypes.PartnerCancelled,
                "Your partner cancelled",
                $"{partnerName} cancelled job #{task.Id}. We are finding you another partner now.",
                "/customer/profile/tasks"), ct);

            var admins = await _context.Users
                .Where(u => UserRoles.AdminRoles.Contains(u.Role) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);

            await _notifier.PushManyAsync(admins.Select(adminId => new NotificationRequest(
                adminId,
                NotificationTypes.PartnerCancelled,
                $"Partner cancelled job #{task.Id}",
                $"{partnerName} cancelled after accepting"
                    + (string.IsNullOrWhiteSpace(reason) ? "." : $": {reason.Trim()}")
                    + " Task reopened and excluded from their board.",
                "/admin/tasks?status=pending")), ct);

            if (bookingMode == TaskBookingMode.Instant) await _offers.StartAsync(task.Id, ct);

            await _realtime.NotifyCategoryPartnersAsync(task.CategoryId, "tasks", ct);
            await _realtime.NotifyAdminsAsync("tasks", ct);
        }

        // POST: api/gigtasks/5/rate  -> Either side rates a finished job
        [HttpPost("{id:int}/rate")]
        public async Task<IActionResult> RateTask(
            int id, RateTaskRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ProblemDetails
                {
                    Title = "Choose between one and five stars.",
                    Status = 400,
                });

            var role = User.IsInRole(UserRoles.Partner) ? RatedBy.Partner : RatedBy.Customer;

            var result = await _ratings.RateAsync(
                User.GetRequiredUserId(), role, id, request.Stars, request.Feedback, ct);

            return result.Succeeded
                ? Ok(GigTaskDto.From(await LoadDetailedAsync(id, ct)))
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
        }

        /// <summary>
        /// Tasks with customer and partner names attached — the shape every
        /// endpoint returns, so responses stay consistent.
        /// </summary>
        private IQueryable<GigTask> DetailedTasks =>
            _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.ServiceItem)
                .Include(t => t.Customer)
                .Include(t => t.Partner)!.ThenInclude(p => p!.User);

        private Task<GigTask> LoadDetailedAsync(int id, CancellationToken ct) =>
            DetailedTasks.FirstAsync(t => t.Id == id, ct);

        private async Task<Partner?> GetCurrentPartnerAsync(CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();
            return await _context.Partners.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        }

        private async Task<bool> CanViewAsync(GigTask task, CancellationToken ct)
        {
            if (User.IsAdmin()) return true;

            var userId = User.GetRequiredUserId();
            if (task.CustomerId == userId) return true;

            if (task.PartnerId is null) return false;

            var partner = await GetCurrentPartnerAsync(ct);
            return partner is not null && task.PartnerId == partner.Id;
        }

        /// <summary>
        /// Returns a failure result when the caller may not make this particular
        /// transition, or null when they may. Cancellation is open to both sides;
        /// progress and completion belong to the assigned partner.
        /// </summary>
        private async Task<ActionResult?> GetPermittedTransitionAsync(
            GigTask task, string newStatus, CancellationToken ct)
        {
            if (User.IsAdmin()) return null;

            var userId = User.GetRequiredUserId();
            var isCustomer = task.CustomerId == userId;

            var partner = User.IsInRole(UserRoles.Partner) ? await GetCurrentPartnerAsync(ct) : null;
            var isAssignedPartner = partner is not null && task.PartnerId == partner.Id;

            if (!isCustomer && !isAssignedPartner) return Forbid();

            if (newStatus == GigTaskStatus.Cancelled) return null;

            // in_progress and completed are the partner's to report.
            return isAssignedPartner
                ? null
                : StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = $"Only the assigned partner can set a task to '{newStatus}'.",
                    Status = 403,
                });
        }
    }
}
