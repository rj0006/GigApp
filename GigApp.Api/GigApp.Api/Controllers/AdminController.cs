using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Earnings;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Menus;
using GigApp.Api.Services.Pricing;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Banking;
using GigApp.Api.Services.Booking;
using GigApp.Api.Services.Orders;
using GigApp.Api.Services.Ratings;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Tracking;
using GigApp.Api.Services.UserAdmin;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// Admin portal. Login only — there is no public admin registration by
    /// design. New admins are seeded or promoted from an existing admin session.
    /// </summary>
    [Route("admin")]
    public class AdminController : PortalControllerBase
    {
        private const string CategoriesPath = "/admin/masters/categories";
        private const string ServicesPath = "/admin/masters/services";
        private const string PricingPath = "/admin/masters/pricing";
        private const string ApprovalsPath = "/admin/approvals";
        private const string MenuPath = "/admin/masters/menu";
        private const string PlansPath = "/admin/masters/plans";
        private const string TaxesPath = "/admin/masters/taxes";

        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;
        private readonly IPriceInsightService _priceInsights;
        private readonly IUserAdminService _userAdmin;
        private readonly IKycHistoryService _kycHistory;
        private readonly IMenuService _menus;
        private readonly IEarningsService _earnings;
        private readonly IPlanService _plans;
        private readonly ITaxService _taxes;
        private readonly ITaskClaimService _claims;
        private readonly IFileStorageService _storage;

        public AdminController(
            IAuthService authService,
            IProfileService profileService,
            IAddressService addressService,
            AppDbContext context,
            ICategoryLookup categories,
            IPriceInsightService priceInsights,
            IUserAdminService userAdmin,
            IKycHistoryService kycHistory,
            IBankAccountService bankAccounts,
            IMenuService menus,
            IEarningsService earnings,
            IPlanService plans,
            ITaxService taxes,
            ITaskClaimService claims,
            IFileStorageService storage,
            IOrderHistoryService orderHistory,
            ISupportService support,
            IRatingService ratings)
            : base(authService, profileService, addressService, bankAccounts, orderHistory, support, ratings)
        {
            _context = context;
            _categories = categories;
            _priceInsights = priceInsights;
            _userAdmin = userAdmin;
            _kycHistory = kycHistory;
            _menus = menus;
            _earnings = earnings;
            _plans = plans;
            _taxes = taxes;
            _claims = claims;
            _storage = storage;
        }

        protected override string PortalSlug => "admin";
        protected override string RequiredRole => UserRoles.Admin;

        // A super admin has strictly more rights than an admin, so both sign in
        // here. Only the super-admin-only screens check the difference.
        protected override bool AcceptsRole(string? role) => UserRoles.IsAdminRole(role);
        protected override bool PrincipalAccepted => User.IsAdmin();

        /// <summary>
        /// The sidebar shows a pending-KYC badge on every page, so the count is
        /// resolved once here rather than in each action.
        /// </summary>
        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (User.IsAdmin())
            {
                // Only partners actually waiting on the administrator. A rejected
                // partner is waiting on themselves, so counting them would keep
                // the badge lit with nothing to do.
                ViewData["PendingKycCount"] = await _context.Partners.CountAsync(
                    p => p.KycStatus == KycStatus.Pending, context.HttpContext.RequestAborted);

                ViewData["OpenEnquiryCount"] = await Support.OpenCountAsync(
                    context.HttpContext.RequestAborted);

                // The sidebar hides the super-admin section for everyone else.
                ViewData["IsSuperAdmin"] = User.IsSuperAdmin();
                ViewData["Sidebar"] = await _menus.GetSidebarAsync(
                    User.IsSuperAdmin(), context.HttpContext.RequestAborted);
            }

            await next();
        }

        [HttpGet("login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl, bool denied = false)
        {
            // Already signed in as an admin — no reason to show the form again.
            if (IsAlreadySignedIn) return RedirectToLocalOr(returnUrl);

            ViewData["Title"] = "Admin sign in";
            return View(BuildLoginModel(returnUrl, denied));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [SkipTracking]   // signing in changes no data
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["Title"] = "Admin sign in";
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

        [HttpPost("logout")]
        [SkipTracking]   // signing out changes no data
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            ClearAuthCookie();
            return Redirect(LoginPath);
        }

        [HttpGet("")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewData["Title"] = "Dashboard";

            // Same ordering as the approvals queue, so the five shown here are the
            // five an administrator would actually act on next.
            var pending = await PartnersWithDetail
                .Where(p => p.KycStatus != KycStatus.Approved)
                .OrderBy(p => p.KycStatus == KycStatus.Pending ? 0
                            : p.KycStatus == KycStatus.NotSubmitted ? 1 : 2)
                .ThenByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync(ct);

            var recentTasks = await TasksWithDetail
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync(ct);

            return View(new AdminDashboardViewModel
            {
                Name = User.Identity?.Name ?? "admin",
                CustomerCount = await _context.Users.CountAsync(u => u.Role == UserRoles.Customer, ct),
                PartnerCount = await _context.Partners.CountAsync(ct),
                PendingKycCount = await _context.Partners.CountAsync(p => p.KycStatus == KycStatus.Pending, ct),
                OpenTaskCount = await _context.GigTasks.CountAsync(t => t.Status == GigTaskStatus.Pending, ct),
                CategoryCount = await _context.SkillCategories.CountAsync(c => c.IsActive, ct),
                PendingPartners = pending.Select(PartnerDto.From).ToList(),
                RecentTasks = recentTasks.Select(GigTaskDto.From).ToList(),
            });
        }

        [HttpGet("masters/categories")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Categories(
            [FromQuery] PageRequest paging, bool showInactive = false, CancellationToken ct = default)
        {
            ViewData["Title"] = "Skill categories";

            var query = _context.SkillCategories.AsNoTracking();
            if (!showInactive) query = query.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(term));
            }

            var page = await query
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new SkillCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    DisplayOrder = c.DisplayOrder,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    PartnerCount = c.Partners.Count,
                    TaskCount = c.Tasks.Count,
                })
                .ToPagedResultAsync(paging, ct);

            return View(new AdminCategoriesViewModel
            {
                Categories = page,
                ShowInactive = showInactive,
                Search = paging.Search,
            });
        }

        [HttpGet("masters/categories/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> NewCategory(CancellationToken ct)
        {
            ViewData["Title"] = "New category";

            // Default to the end of the list so a new row does not jump the order.
            var nextOrder = await _context.SkillCategories.AnyAsync(ct)
                ? await _context.SkillCategories.MaxAsync(c => c.DisplayOrder, ct) + 1
                : 0;

            return View("CategoryForm", new SkillCategoryFormViewModel
            {
                Form = new SaveSkillCategoryRequest { IsActive = true, DisplayOrder = nextOrder },
            });
        }

        [HttpPost("masters/categories/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewCategory(SaveSkillCategoryRequest form, CancellationToken ct)
        {
            ViewData["Title"] = "New category";
            var model = new SkillCategoryFormViewModel { Form = form };

            if (!ModelState.IsValid) return View("CategoryForm", model);

            var name = form.Name.Trim();

            if (await NameExistsAsync(name, excludingId: null, ct))
            {
                ModelState.AddModelError(nameof(form.Name), $"'{name}' already exists.");
                return View("CategoryForm", model);
            }

            var category = new SkillCategory
            {
                Name = name,
                Description = Normalize(form.Description),
                IsActive = form.IsActive,
                DisplayOrder = form.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
            };

            var imageError = await ApplyCategoryImageAsync(form.Image, category, ct);
            if (imageError is not null)
            {
                ModelState.AddModelError(nameof(form.Image), imageError);
                return View("CategoryForm", model);
            }

            _context.SkillCategories.Add(category);
            await _context.SaveChangesAsync(ct);

            TempData["Success"] = $"Category '{name}' created.";
            return Redirect(CategoriesPath);
        }

        [HttpGet("masters/categories/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> EditCategory(int id, CancellationToken ct)
        {
            ViewData["Title"] = "Edit category";

            var category = await _context.SkillCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category is null)
            {
                TempData["Error"] = "Category not found.";
                return Redirect(CategoriesPath);
            }

            return View("CategoryForm", new SkillCategoryFormViewModel
            {
                Id = category.Id,
                Form = new SaveSkillCategoryRequest
                {
                    Name = category.Name,
                    Description = category.Description,
                    IsActive = category.IsActive,
                    DisplayOrder = category.DisplayOrder,
                },
                ImageUrl = SkillCategoryDto.From(category).ImageUrl,
                PartnerCount = await _context.Partners.CountAsync(p => p.SkillCategoryId == id, ct),
                TaskCount = await _context.GigTasks.CountAsync(t => t.CategoryId == id, ct),
            });
        }

        [HttpPost("masters/categories/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, SaveSkillCategoryRequest form, CancellationToken ct)
        {
            ViewData["Title"] = "Edit category";
            var model = new SkillCategoryFormViewModel { Id = id, Form = form };

            if (!ModelState.IsValid) return View("CategoryForm", model);

            var category = await _context.SkillCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null)
            {
                TempData["Error"] = "Category not found.";
                return Redirect(CategoriesPath);
            }

            var name = form.Name.Trim();

            if (await NameExistsAsync(name, excludingId: id, ct))
            {
                ModelState.AddModelError(nameof(form.Name), $"'{name}' already exists.");
                return View("CategoryForm", model);
            }

            var imageError = await ApplyCategoryImageAsync(form.Image, category, ct);
            if (imageError is not null)
            {
                ModelState.AddModelError(nameof(form.Image), imageError);
                return View("CategoryForm", model);
            }

            category.Name = name;
            category.Description = Normalize(form.Description);
            category.IsActive = form.IsActive;
            category.DisplayOrder = form.DisplayOrder;
            category.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            TempData["Success"] = $"Category '{name}' updated.";
            return Redirect(CategoriesPath);
        }

        [HttpPost("masters/categories/{id:int}/toggle")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCategory(int id, bool isActive, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "That request could not be read. Reload the page and try again.";
                return Redirect(CategoriesPath);
            }

            var category = await _context.SkillCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category is null)
            {
                TempData["Error"] = "Category not found.";
            }
            else
            {
                category.IsActive = isActive;
                category.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                TempData["Success"] = isActive
                    ? $"'{category.Name}' is active and selectable again."
                    : $"'{category.Name}' deactivated. Existing partners and tasks keep it.";
            }

            return Redirect(CategoriesPath);
        }

        [HttpPost("masters/categories/{id:int}/delete")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
        {
            var category = await _context.SkillCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category is null)
            {
                TempData["Error"] = "Category not found.";
                return Redirect(CategoriesPath);
            }

            var partnerCount = await _context.Partners.CountAsync(p => p.SkillCategoryId == id, ct);
            var taskCount = await _context.GigTasks.CountAsync(t => t.CategoryId == id, ct);

            if (partnerCount > 0 || taskCount > 0)
            {
                TempData["Error"] =
                    $"'{category.Name}' is used by {partnerCount} partner(s) and {taskCount} task(s). "
                  + "Deactivate it instead of deleting it.";
            }
            else
            {
                _context.SkillCategories.Remove(category);
                await _context.SaveChangesAsync(ct);
                TempData["Success"] = $"Category '{category.Name}' deleted.";
            }

            return Redirect(CategoriesPath);
        }

        [HttpGet("masters/services")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Services(
            [FromQuery] PageRequest paging,
            int? categoryId,
            bool showInactive = false,
            CancellationToken ct = default)
        {
            ViewData["Title"] = "Services";

            var query = _context.ServiceItems.AsNoTracking().Include(s => s.SkillCategory).AsQueryable();

            if (!showInactive) query = query.Where(s => s.IsActive);
            if (categoryId is not null) query = query.Where(s => s.SkillCategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term));
            }

            var page = await query
                .OrderBy(s => s.SkillCategory!.DisplayOrder).ThenBy(s => s.SkillCategory!.Name)
                .ThenBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .Select(s => new ServiceItemDto
                {
                    Id = s.Id,
                    SkillCategoryId = s.SkillCategoryId,
                    CategoryName = s.SkillCategory!.Name,
                    Name = s.Name,
                    Description = s.Description,
                    BasePayout = s.BasePayout,
                    AllowsInstantBooking = s.AllowsInstantBooking,
                    IsActive = s.IsActive,
                    DisplayOrder = s.DisplayOrder,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    TaskCount = s.Tasks.Count,
                })
                .ToPagedResultAsync(paging, ct);

            return View(new AdminServiceItemsViewModel
            {
                Items = page,
                Categories = await _categories.GetActiveOptionsAsync(ct),
                CategoryFilter = categoryId,
                ShowInactive = showInactive,
            });
        }

        [HttpGet("masters/services/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> NewService(int? categoryId, CancellationToken ct)
        {
            ViewData["Title"] = "New service";

            return View("ServiceForm", new ServiceItemFormViewModel
            {
                Form = new SaveServiceItemRequest
                {
                    IsActive = true,
                    SkillCategoryId = categoryId ?? 0,
                    DisplayOrder = await NextServiceOrderAsync(categoryId, ct),
                },
                Categories = await _categories.GetActiveOptionsAsync(ct),
            });
        }

        [HttpPost("masters/services/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("ServiceItem")]
        public async Task<IActionResult> NewService(SaveServiceItemRequest form, CancellationToken ct)
        {
            ViewData["Title"] = "New service";

            var model = new ServiceItemFormViewModel
            {
                Form = form,
                Categories = await _categories.GetActiveOptionsAsync(ct),
            };

            var error = await ValidateServiceAsync(form, excludingId: null, ct);
            if (error is not null) ModelState.AddModelError(string.Empty, error);
            if (!ModelState.IsValid) return View("ServiceForm", model);

            var item = new ServiceItem
            {
                SkillCategoryId = form.SkillCategoryId,
                Name = form.Name.Trim(),
                Description = Normalize(form.Description),
                BasePayout = form.BasePayout,
                AllowsInstantBooking = form.AllowsInstantBooking && form.BasePayout is > 0,
                IsActive = form.IsActive,
                DisplayOrder = form.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
            };

            _context.ServiceItems.Add(item);
            await _context.SaveChangesAsync(ct);

            TrackDoc(item.Id, ServiceItemDto.From(item));
            TempData["Success"] = $"Service '{item.Name}' created.";
            return Redirect(ServicesPath);
        }

        [HttpGet("masters/services/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> EditService(int id, CancellationToken ct)
        {
            ViewData["Title"] = "Edit service";

            var item = await _context.ServiceItems.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (item is null)
            {
                TempData["Error"] = "Service not found.";
                return Redirect(ServicesPath);
            }

            return View("ServiceForm", new ServiceItemFormViewModel
            {
                Id = item.Id,
                Form = new SaveServiceItemRequest
                {
                    SkillCategoryId = item.SkillCategoryId,
                    Name = item.Name,
                    Description = item.Description,
                    BasePayout = item.BasePayout,
                    AllowsInstantBooking = item.AllowsInstantBooking,
                    IsActive = item.IsActive,
                    DisplayOrder = item.DisplayOrder,
                },
                Categories = await _categories.GetOptionsIncludingAsync(item.SkillCategoryId, ct),
                TaskCount = await _context.GigTasks.CountAsync(t => t.ServiceItemId == id, ct),
            });
        }

        [HttpPost("masters/services/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("ServiceItem")]
        public async Task<IActionResult> EditService(int id, SaveServiceItemRequest form, CancellationToken ct)
        {
            ViewData["Title"] = "Edit service";

            var model = new ServiceItemFormViewModel
            {
                Id = id,
                Form = form,
                Categories = await _categories.GetOptionsIncludingAsync(form.SkillCategoryId, ct),
            };

            var error = await ValidateServiceAsync(form, excludingId: id, ct);
            if (error is not null) ModelState.AddModelError(string.Empty, error);
            if (!ModelState.IsValid) return View("ServiceForm", model);

            var item = await _context.ServiceItems.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (item is null)
            {
                TempData["Error"] = "Service not found.";
                return Redirect(ServicesPath);
            }

            item.SkillCategoryId = form.SkillCategoryId;
            item.Name = form.Name.Trim();
            item.Description = Normalize(form.Description);
            item.BasePayout = form.BasePayout;
            item.AllowsInstantBooking = form.AllowsInstantBooking && form.BasePayout is > 0;
            item.IsActive = form.IsActive;
            item.DisplayOrder = form.DisplayOrder;
            item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            TrackDoc(item.Id, ServiceItemDto.From(item));
            TempData["Success"] = $"Service '{item.Name}' updated.";
            return Redirect(ServicesPath);
        }

        [HttpPost("masters/services/{id:int}/toggle")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("ServiceItem")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> ToggleService(int id, bool isActive, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "That request could not be read. Reload the page and try again.";
                return Redirect(ServicesPath);
            }

            var item = await _context.ServiceItems.FirstOrDefaultAsync(s => s.Id == id, ct);

            if (item is null)
            {
                TempData["Error"] = "Service not found.";
            }
            else
            {
                item.IsActive = isActive;
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                TrackDoc(item.Id, ServiceItemDto.From(item));
                TempData["Success"] = isActive
                    ? $"'{item.Name}' is selectable again."
                    : $"'{item.Name}' deactivated. Existing tasks keep it.";
            }

            return Redirect(ServicesPath);
        }

        [HttpPost("masters/services/{id:int}/delete")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("ServiceItem")]
        public async Task<IActionResult> DeleteService(int id, CancellationToken ct)
        {
            var item = await _context.ServiceItems.FirstOrDefaultAsync(s => s.Id == id, ct);

            if (item is null)
            {
                TempData["Error"] = "Service not found.";
                return Redirect(ServicesPath);
            }

            var taskCount = await _context.GigTasks.CountAsync(t => t.ServiceItemId == id, ct);

            if (taskCount > 0)
            {
                TempData["Error"] =
                    $"'{item.Name}' is used by {taskCount} task(s). Deactivate it instead of deleting it.";
            }
            else
            {
                _context.ServiceItems.Remove(item);
                await _context.SaveChangesAsync(ct);
                TempData["Success"] = $"Service '{item.Name}' deleted.";
            }

            return Redirect(ServicesPath);
        }

        [HttpGet("masters/pricing")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Pricing(CancellationToken ct)
        {
            ViewData["Title"] = "Price insights";

            return View(new AdminPriceInsightsViewModel
            {
                Insights = await _priceInsights.GetAsync(ct),
            });
        }

        [HttpPost("masters/pricing/{id:int}/apply")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("ServiceItem")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> ApplyMedianPayout(int id, CancellationToken ct)
        {
            var insight = await _priceInsights.GetForItemAsync(id, ct);

            if (insight?.MedianAmount is null)
            {
                TempData["Error"] = "There is no completed work to price this from yet.";
                return Redirect(PricingPath);
            }

            // Refuse below the sample threshold — a median over a handful of
            // jobs is noise, and locking a price to it would be worse than
            // leaving it unset.
            if (!insight.HasEnoughData)
            {
                TempData["Error"] =
                    $"Only {insight.CompletedCount} completed job(s). At least "
                  + $"{PriceInsightDto.MinimumSampleSize} are needed before this median means anything.";

                return Redirect(PricingPath);
            }

            var item = await _context.ServiceItems.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (item is null)
            {
                TempData["Error"] = "Service not found.";
                return Redirect(PricingPath);
            }

            item.BasePayout = insight.MedianAmount;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            TrackDoc(item.Id, ServiceItemDto.From(item));
            TempData["Success"] =
                $"'{item.Name}' payout set to ₹{insight.MedianAmount:N0} from {insight.CompletedCount} completed job(s).";

            return Redirect(PricingPath);
        }

        [HttpGet("masters/menu")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Menus(CancellationToken ct)
        {
            ViewData["Title"] = "Menu";

            return View("Menus", new AdminMenusViewModel
            {
                Items = await _menus.GetAllAsync(ct),
            });
        }

        [HttpGet("masters/menu/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> NewMenu(CancellationToken ct)
        {
            ViewData["Title"] = "New menu item";

            return View("MenuForm", new MenuFormViewModel
            {
                Form = new SaveMenuItemRequest { IsActive = true, SortOrder = 1 },
                Parents = await _menus.GetParentOptionsAsync(null, ct),
            });
        }

        [HttpPost("masters/menu/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Menu")]
        public Task<IActionResult> NewMenu(SaveMenuItemRequest form, CancellationToken ct) =>
            SaveMenuAsync(null, form, "New menu item", ct);

        [HttpGet("masters/menu/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> EditMenu(int id, CancellationToken ct)
        {
            var item = await _menus.GetAsync(id, ct);
            if (item is null) return NotFound();

            ViewData["Title"] = "Edit menu item";

            return View("MenuForm", new MenuFormViewModel
            {
                Id = id,
                Form = SaveMenuItemRequest.From(item),
                Parents = await _menus.GetParentOptionsAsync(id, ct),
            });
        }

        [HttpPost("masters/menu/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Menu")]
        public Task<IActionResult> EditMenu(int id, SaveMenuItemRequest form, CancellationToken ct) =>
            SaveMenuAsync(id, form, "Edit menu item", ct);

        [HttpPost("masters/menu/{id:int}/delete")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Menu")]
        [TrackEntry(TrackingEntryType.Delete)]
        public async Task<IActionResult> DeleteMenu(int id, CancellationToken ct)
        {
            var result = await _menus.DeleteAsync(id, ct);

            if (result.Succeeded)
            {
                TrackDoc(id, result.Item);
                TempData["Success"] = $"'{result.Item!.Label}' removed from the menu.";
            }
            else
            {
                TempData["Error"] = result.Error;
            }

            return Redirect(MenuPath);
        }

        private async Task<IActionResult> SaveMenuAsync(
            int? id, SaveMenuItemRequest form, string title, CancellationToken ct)
        {
            ViewData["Title"] = title;

            if (!ModelState.IsValid)
            {
                return View("MenuForm", new MenuFormViewModel
                {
                    Id = id,
                    Form = form,
                    Parents = await _menus.GetParentOptionsAsync(id, ct),
                });
            }

            var result = await _menus.SaveAsync(id, form, ct);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error!);

                return View("MenuForm", new MenuFormViewModel
                {
                    Id = id,
                    Form = form,
                    Parents = await _menus.GetParentOptionsAsync(id, ct),
                });
            }

            TrackDoc(result.Item!.Id, result.Item);
            TempData["Success"] = $"'{result.Item.Label}' saved.";
            return Redirect(MenuPath);
        }

        [HttpGet("approvals")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Approvals([FromQuery] PageRequest paging, CancellationToken ct)
        {
            ViewData["Title"] = "Partner approvals";

            // Anyone actually waiting on the administrator comes first. Rejected
            // partners stay on the list so their status is visible, but the ball
            // is in their court, so they sink to the bottom.
            var page = await PartnersWithDetail
                .Where(p => p.KycStatus != KycStatus.Approved)
                .OrderBy(p => p.KycStatus == KycStatus.Pending ? 0
                            : p.KycStatus == KycStatus.NotSubmitted ? 1 : 2)
                .ThenByDescending(p => p.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return View(new AdminPartnersViewModel
            {
                Partners = page.Map(PartnerDto.From),
                Categories = await _categories.GetActiveOptionsAsync(ct),
                VerifiedFilter = false,
                KycHistory = await _kycHistory.ForPartnersAsync(
                    page.Items.Select(p => p.Id).ToList(), ct),
            });
        }

        [HttpPost("approvals/{id:int}/verify")]
        [Authorize(Policy = Policies.AdminOnly)]
        [TrackForm(KycHistoryService.FormType)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetVerification(
            int id, bool isVerified, string? reason, string? returnTo, CancellationToken ct)
        {
            // A malformed value would bind to default(bool) and quietly perform the
            // opposite action, so refuse rather than guess.
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "That request could not be read. Reload the page and try again.";
                return Redirect(ApprovalsPath);
            }

            var target = !string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo) ? returnTo : ApprovalsPath;

            var partner = await _context.Partners
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (partner is null)
            {
                TempData["Error"] = "Partner not found.";
            }
            else if (isVerified && !partner.HasCompleteKyc)
            {
                TempData["Error"] =
                    $"{partner.User?.Name} has not submitted a selfie, both Aadhaar sides and an Aadhaar number yet.";
            }
            else if (!isVerified && string.IsNullOrWhiteSpace(reason))
            {
                // Rejecting without saying why leaves the partner with no idea
                // what to fix, so the reason is mandatory.
                TempData["Error"] = "Enter a reason before rejecting.";
            }
            else
            {
                PartnerKyc.Review(partner, isVerified, reason);

                await _context.SaveChangesAsync(ct);

                // `reason` is picked up as the audit Remark by TrackingActionFilter.
                TrackDoc(partner.Id, PartnerDto.From(partner));
                TempData["Success"] = isVerified
                    ? $"{partner.User?.Name} is now verified."
                    : $"{partner.User?.Name} was rejected. They can fix the issue and resubmit.";
            }

            return Redirect(target);
        }

        // These are the operations that must not be delegated. A team member
        // with the plain admin role reaches everything above but not this.

        [HttpPost("users/{id:int}/password")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("UserAccount")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> ResetUserPassword(
            int id, AdminResetPasswordRequest form, string? returnTo, CancellationToken ct)
        {
            var target = LocalOr(returnTo, "/admin/users/customers");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = FirstError() ?? "Could not reset that password.";
                return Redirect(target);
            }

            var result = await _userAdmin.ResetPasswordAsync(
                User.GetRequiredUserId(), id, form, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return Redirect(target);
            }

            TrackDoc(id, result.User);

            // Shown once. It is not stored anywhere and the audit payload masks
            // it, so there is no second chance to read it.
            TempData["Success"] = result.WasGenerated
                ? $"Password reset for {result.User!.Name}. Generated password: {result.GeneratedPassword} "
                  + "— copy it now, it will not be shown again."
                : $"Password reset for {result.User!.Name} to the one you typed.";

            return Redirect(target);
        }

        [HttpPost("users/{id:int}/active")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("UserAccount")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> SetUserActive(
            int id, SetUserActiveRequest form, string? returnTo, CancellationToken ct)
        {
            var target = LocalOr(returnTo, "/admin/users/customers");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "That request could not be read. Reload the page and try again.";
                return Redirect(target);
            }

            var result = await _userAdmin.SetActiveAsync(User.GetRequiredUserId(), id, form, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return Redirect(target);
            }

            TrackDoc(id, result.User);
            TempData["Success"] = form.IsActive
                ? $"{result.User!.Name} can sign in again."
                : $"{result.User!.Name} has been deactivated and signed out everywhere.";

            return Redirect(target);
        }

        private string LocalOr(string? returnTo, string fallback) =>
            !string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo) ? returnTo : fallback;

        [HttpGet("users/partners")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Partners(
            [FromQuery] PageRequest paging, bool? verified, int? categoryId, CancellationToken ct = default)
        {
            ViewData["Title"] = "Partners";

            var query = PartnersWithDetail;

            if (verified is not null) query = query.Where(p => (verified == true ? p.KycStatus == KycStatus.Approved : p.KycStatus != KycStatus.Approved));
            if (categoryId is not null) query = query.Where(p => p.SkillCategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(p =>
                    p.User!.Name.ToLower().Contains(term) || p.User.Phone.Contains(term));
            }

            var page = await query
                .OrderBy(p => p.KycStatus == KycStatus.Approved ? 1 : 0).ThenByDescending(p => p.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return View(new AdminPartnersViewModel
            {
                Partners = page.Map(PartnerDto.From),
                Categories = await _categories.GetActiveOptionsAsync(ct),
                VerifiedFilter = verified,
                CategoryFilter = categoryId,
                // This is the user-management list, so a super admin gets the
                // password and activation actions against the same rows.
                Accounts = page.Items
                    .Where(p => p.User is not null)
                    .ToDictionary(p => p.Id, p => UserDto.From(p.User!)),
                KycHistory = await _kycHistory.ForPartnersAsync(
                    page.Items.Select(p => p.Id).ToList(), ct),
            });
        }

        [HttpGet("users/customers")]
        [Authorize(Policy = Policies.AdminOnly)]
        public Task<IActionResult> Customers([FromQuery] PageRequest paging, CancellationToken ct) =>
            UserListAsync(UserRoles.Customer, "Customers", paging, ct);

        [HttpGet("users/admins")]
        [Authorize(Policy = Policies.AdminOnly)]
        public Task<IActionResult> Admins([FromQuery] PageRequest paging, CancellationToken ct) =>
            UserListAsync(UserRoles.Admin, "Administrators", paging, ct, includeSuperAdmins: true);

        [HttpGet("masters/plans")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Plans(bool showInactive = false, CancellationToken ct = default)
        {
            ViewData["Title"] = "Commission plans";

            return View(new AdminPlansViewModel
            {
                Plans = await _plans.GetPlansAsync(!showInactive, ct),
                ShowInactive = showInactive,
            });
        }

        [HttpGet("masters/plans/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult NewPlan()
        {
            ViewData["Title"] = "New plan";
            return View("PlanForm", new PlanFormViewModel { Form = new SaveCommissionPlanRequest() });
        }

        [HttpPost("masters/plans/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("CommissionPlan")]
        public Task<IActionResult> NewPlan(SaveCommissionPlanRequest form, CancellationToken ct) =>
            SavePlanAsync(null, form, "New plan", ct);

        [HttpGet("masters/plans/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> EditPlan(int id, CancellationToken ct)
        {
            var plan = await _plans.GetPlanAsync(id, ct);
            if (plan is null) return NotFound();

            ViewData["Title"] = "Edit plan";

            return View("PlanForm", new PlanFormViewModel
            {
                Id = id,
                Form = SaveCommissionPlanRequest.From(plan),
            });
        }

        [HttpPost("masters/plans/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("CommissionPlan")]
        public Task<IActionResult> EditPlan(int id, SaveCommissionPlanRequest form, CancellationToken ct) =>
            SavePlanAsync(id, form, "Edit plan", ct);

        private async Task<IActionResult> SavePlanAsync(
            int? id, SaveCommissionPlanRequest form, string title, CancellationToken ct)
        {
            ViewData["Title"] = title;
            var model = new PlanFormViewModel { Id = id, Form = form };

            if (!ModelState.IsValid) return View("PlanForm", model);

            var result = await _plans.SavePlanAsync(id, form, ct);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View("PlanForm", model);
            }

            TrackDoc(result.Plan!.Id, result.Plan);
            TempData["Success"] = $"'{result.Plan.Name}' saved.";
            return Redirect(PlansPath);
        }

        [HttpPost("payouts/{id:int}/plan")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("CommissionPlan")]
        public async Task<IActionResult> AssignPlan(int id, AssignPlanRequest form, CancellationToken ct)
        {
            var target = $"/admin/payouts/{id}";

            if (!ModelState.IsValid)
            {
                TempData["Error"] = FirstError() ?? "Could not change the plan.";
                return Redirect(target);
            }

            var result = await _plans.AssignPlanAsync(
                id, form, User.GetRequiredUserId(), User.Identity?.Name, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return Redirect(target);
            }

            if (result.ChargeFee)
            {
                await _earnings.PostSubscriptionFeeAsync(
                    id, result.PlanId, User.GetRequiredUserId(), User.Identity?.Name, ct);
            }

            TrackDoc(id, PartnerPlanDto.From(result.Subscription!));
            TempData["Success"] = "Plan changed. It applies to jobs completed from now on.";
            return Redirect(target);
        }

        [HttpGet("masters/taxes")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Taxes(
            string? country, bool showInactive = false, CancellationToken ct = default)
        {
            ViewData["Title"] = "Tax rules";

            return View(new AdminTaxesViewModel
            {
                Rules = await _taxes.GetRulesAsync(country, !showInactive, ct),
                CountryFilter = country,
                ShowInactive = showInactive,
            });
        }

        [HttpGet("masters/taxes/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult NewTax()
        {
            ViewData["Title"] = "New tax rule";
            return View("TaxForm", new TaxFormViewModel { Form = new SaveTaxRuleRequest() });
        }

        [HttpPost("masters/taxes/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("TaxRule")]
        public Task<IActionResult> NewTax(SaveTaxRuleRequest form, CancellationToken ct) =>
            SaveTaxAsync(null, form, "New tax rule", ct);

        [HttpGet("masters/taxes/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> EditTax(int id, CancellationToken ct)
        {
            var rule = await _taxes.GetRuleAsync(id, ct);
            if (rule is null) return NotFound();

            ViewData["Title"] = "Edit tax rule";

            return View("TaxForm", new TaxFormViewModel
            {
                Id = id,
                Form = SaveTaxRuleRequest.From(rule),
            });
        }

        [HttpPost("masters/taxes/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("TaxRule")]
        public Task<IActionResult> EditTax(int id, SaveTaxRuleRequest form, CancellationToken ct) =>
            SaveTaxAsync(id, form, "Edit tax rule", ct);

        private async Task<IActionResult> SaveTaxAsync(
            int? id, SaveTaxRuleRequest form, string title, CancellationToken ct)
        {
            ViewData["Title"] = title;
            var model = new TaxFormViewModel { Id = id, Form = form };

            if (!ModelState.IsValid) return View("TaxForm", model);

            var result = await _taxes.SaveRuleAsync(id, form, ct);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View("TaxForm", model);
            }

            TrackDoc(result.Rule!.Id, result.Rule);
            TempData["Success"] = $"'{result.Rule.Name}' saved.";
            return Redirect(TaxesPath);
        }

        [HttpGet("payouts")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Payouts(CancellationToken ct)
        {
            ViewData["Title"] = "Partner payouts";

            return View(new AdminPayoutsViewModel
            {
                Balances = await _earnings.GetBalancesAsync(ct),
            });
        }

        [HttpGet("payouts/{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> PartnerLedger(
            int id, [FromQuery] PageRequest paging, string? entryType, CancellationToken ct = default)
        {
            var partner = await PartnersWithDetail.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (partner is null) return NotFound();

            ViewData["Title"] = $"{partner.User?.Name} — earnings";

            return View(new AdminPartnerLedgerViewModel
            {
                Partner = PartnerDto.From(partner),
                Summary = await _earnings.GetSummaryAsync(id, ct),
                Entries = await _earnings.GetEntriesAsync(id, paging, entryType, ct),
                BankAccount = await BankAccounts.GetAsync(partner.UserId, ct),
                CurrentPlan = await _plans.GetPartnerPlanAsync(id, ct),
                AvailablePlans = await _plans.GetPlansAsync(true, ct),
                PlanHistory = await _plans.GetPartnerPlanHistoryAsync(id, ct),
            });
        }

        [HttpPost("payouts/{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Payout")]
        public async Task<IActionResult> RecordPayout(
            int id, RecordPayoutRequest payoutForm, CancellationToken ct)
        {
            var target = $"/admin/payouts/{id}";

            if (!ModelState.IsValid)
            {
                TempData["Error"] = FirstError() ?? "Could not record that payout.";
                return Redirect(target);
            }

            var result = await _earnings.PostPayoutAsync(
                id, payoutForm, User.GetRequiredUserId(), User.Identity?.Name, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return Redirect(target);
            }

            TrackDoc(result.Entry!.Id, result.Entry);
            TempData["Success"] = $"Payout of {payoutForm.Amount:N2} recorded.";
            return Redirect(target);
        }

        [HttpPost("payouts/{id:int}/adjust")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Payout")]
        public async Task<IActionResult> RecordAdjustment(
            int id, RecordAdjustmentRequest form, CancellationToken ct)
        {
            var target = $"/admin/payouts/{id}";

            if (!ModelState.IsValid)
            {
                TempData["Error"] = FirstError() ?? "Could not record that adjustment.";
                return Redirect(target);
            }

            var result = await _earnings.PostAdjustmentAsync(
                id, form, User.GetRequiredUserId(), User.Identity?.Name, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return Redirect(target);
            }

            TrackDoc(result.Entry!.Id, result.Entry);
            TempData["Success"] = "Adjustment recorded.";
            return Redirect(target);
        }

        [HttpGet("errors")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> Errors(
            [FromQuery] PageRequest paging, bool showResolved = false, CancellationToken ct = default)
        {
            ViewData["Title"] = "Error log";

            var query = _context.ErrorLogs.AsNoTracking();
            if (!showResolved) query = query.Where(l => !l.IsResolved);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(l => l.Reference.ToLower().Contains(term)
                                      || l.Message.ToLower().Contains(term)
                                      || (l.Module != null && l.Module.ToLower().Contains(term)));
            }

            var page = await query.OrderByDescending(l => l.OccurredAt).ToPagedResultAsync(paging, ct);

            return View(new AdminErrorLogsViewModel
            {
                Logs = page.Map(ErrorLogDto.From),
                ShowResolved = showResolved,
            });
        }

        [HttpPost("errors/{id:long}/resolve")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("ErrorLog")]
        public async Task<IActionResult> ResolveError(long id, string? note, CancellationToken ct)
        {
            var log = await _context.ErrorLogs.FirstOrDefaultAsync(l => l.Id == id, ct);

            if (log is null)
            {
                TempData["Error"] = "That entry no longer exists.";
            }
            else
            {
                log.IsResolved = true;
                log.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                log.ResolvedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                TrackDoc(log.Id, ErrorLogDto.From(log));
                TempData["Success"] = $"{log.Reference} marked as resolved.";
            }

            return Redirect("/admin/errors");
        }

        [HttpGet("tasks")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Tasks(
            [FromQuery] PageRequest paging, string? status, int? categoryId, CancellationToken ct = default)
        {
            ViewData["Title"] = "Tasks";

            var query = TasksWithDetail;

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

            return View(new AdminTasksViewModel
            {
                Tasks = page.Map(GigTaskDto.From),
                Categories = await _categories.GetActiveOptionsAsync(ct),
                StatusFilter = status,
                CategoryFilter = categoryId,
            });
        }

        private async Task<string?> ApplyCategoryImageAsync(
            IFormFile? image, SkillCategory category, CancellationToken ct)
        {
            if (image is null || image.Length == 0) return null;

            var saved = await _storage.SaveAsync(image, FileCategory.CategoryImage, ct);
            if (!saved.Succeeded) return saved.Error;

            var previous = category.ImageFileName;
            category.ImageFileName = saved.FileName;

            _storage.Delete(previous, FileCategory.CategoryImage);

            return null;
        }

        [HttpGet("enquiries")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Enquiries(
            [FromQuery] PageRequest paging, string? status, CancellationToken ct = default)
        {
            ViewData["Title"] = "Support enquiries";

            return View(new AdminEnquiriesViewModel
            {
                Enquiries = await Support.ListAsync(paging, status, ct),
                StatusFilter = status,
            });
        }

        [HttpPost("enquiries/{id:int}/review")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("SupportEnquiry")]
        public async Task<IActionResult> ReviewEnquiry(
            int id, ResolveEnquiryRequest form, CancellationToken ct = default)
        {
            var result = await Support.ReviewAsync(User.GetRequiredUserId(), id, form, ct);

            if (result.Succeeded)
            {
                TrackDoc(id, result.Enquiry!);
                TempData["Success"] = result.Enquiry!.Status == EnquiryStatus.Resolved
                    ? $"{result.Enquiry.Reference} resolved. The person who raised it can now see your reply."
                    : $"{result.Enquiry.Reference} marked as being looked at.";
            }
            else
            {
                TempData["Error"] = result.Error;
            }

            return Redirect("/admin/enquiries");
        }

        [HttpPost("tasks/{id:int}/assign")]
        [Authorize(Policy = Policies.AdminOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("TaskAssignment")]
        public async Task<IActionResult> AssignTask(
            int id, int partnerId, string? note, CancellationToken ct = default)
        {
            var result = await _claims.AssignAsync(
                User.GetRequiredUserId(), id, partnerId, note ?? string.Empty, ct);

            if (result.Succeeded)
                TempData["Success"] = $"Task #{id} assigned at ₹{result.Amount:N0}. Any open bids on it were closed.";
            else
                TempData["Error"] = result.Error;

            return Redirect("/admin/tasks");
        }

        private IQueryable<Partner> PartnersWithDetail =>
            _context.Partners.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory);

        private IQueryable<GigTask> TasksWithDetail =>
            _context.GigTasks.AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.ServiceItem)
                .Include(t => t.Customer)
                .Include(t => t.AssignedBy)
                .Include(t => t.Partner)!.ThenInclude(p => p!.User);

        private async Task<IActionResult> UserListAsync(
            string role, string heading, PageRequest paging, CancellationToken ct,
            bool includeSuperAdmins = false)
        {
            ViewData["Title"] = heading;

            var query = _context.Users.AsNoTracking()
                .Include(u => u.PartnerProfile)!.ThenInclude(p => p!.SkillCategory)
                // Super admins are administrators too, so they belong on that
                // list rather than being invisible.
                .Where(u => u.Role == role
                         || (includeSuperAdmins && u.Role == UserRoles.SuperAdmin));

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(u =>
                    u.Name.ToLower().Contains(term) ||
                    u.Phone.Contains(term) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)));
            }

            var page = await query.OrderByDescending(u => u.CreatedAt).ToPagedResultAsync(paging, ct);

            return View("Users", new AdminUsersViewModel
            {
                Role = role,
                Heading = heading,
                Users = page.Map(UserDto.From),
                Search = paging.Search,
            });
        }

        /// <summary>Returns the problem with this service, or null when it is fine.</summary>
        private async Task<string?> ValidateServiceAsync(
            SaveServiceItemRequest form, int? excludingId, CancellationToken ct)
        {
            if (!await _categories.IsSelectableAsync(form.SkillCategoryId, ct))
                return "Choose an active category.";

            var name = form.Name.Trim().ToLower();

            var duplicate = await _context.ServiceItems.AnyAsync(
                s => s.SkillCategoryId == form.SkillCategoryId
                  && s.Name.ToLower() == name
                  && (excludingId == null || s.Id != excludingId), ct);

            if (duplicate)
                return $"'{form.Name.Trim()}' already exists in that category.";

            // Instant booking means charging a set amount, so there has to be one.
            if (form.AllowsInstantBooking && form.BasePayout is null or <= 0)
                return "Set a partner payout before allowing instant booking.";

            return null;
        }

        private async Task<int> NextServiceOrderAsync(int? categoryId, CancellationToken ct)
        {
            if (categoryId is null) return 0;

            var query = _context.ServiceItems.Where(s => s.SkillCategoryId == categoryId);
            return await query.AnyAsync(ct) ? await query.MaxAsync(s => s.DisplayOrder, ct) + 1 : 0;
        }

        private Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken ct) =>
            _context.SkillCategories.AnyAsync(
                c => c.Name.ToLower() == name.ToLower() && (excludingId == null || c.Id != excludingId), ct);

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
