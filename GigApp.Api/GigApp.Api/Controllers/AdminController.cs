using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Menus;
using GigApp.Api.Services.Pricing;
using GigApp.Api.Services.Notifications;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Tracking;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
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
        private const string PricingPath = "/admin/masters/pricing";

        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;
        private readonly IPriceInsightService _priceInsights;
        private readonly IMenuService _menus;
        private readonly IAuthSettingsService _authSettings;

        public AdminController(
            IAuthService authService,
            IRefreshTokenService refreshTokens,
            IProfileService profileService,
            AppDbContext context,
            ICategoryLookup categories,
            IPriceInsightService priceInsights,
            IMenuService menus,
            ISupportService support,
            INotificationService notifier,
            IAuthSettingsService authSettings)
            : base(authService, refreshTokens, profileService, support, notifier)
        {
            _context = context;
            _categories = categories;
            _priceInsights = priceInsights;
            _menus = menus;
            _authSettings = authSettings;
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

            // The base fills the notification bell, so it has to run the rest.
            await base.OnActionExecutionAsync(context, next);
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
        [EnableRateLimiting(RateLimiterPolicies.Auth)]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ProblemDetails { Title = FirstError() ?? "Enter your details and try again.", Status = 400 });

            var failed = await SignInJsonAsync(() => AuthService.LoginAsync(new LoginRequest
            {
                Identifier = model.Identifier,
                Password = model.Password,
                Role = RequiredRole,
            }));

            if (failed is not null) return failed;

            return Ok(new { redirectTo = LocalRedirectTarget(model.ReturnUrl) });
        }

        [HttpPost("logout")]
        [SkipTracking]   // signing out changes no data
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            await ClearAuthCookieAsync(ct);
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
        public IActionResult Categories()
        {
            ViewData["Title"] = "Skill categories";
            return View();
        }

        [HttpGet("masters/categories/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult NewCategory()
        {
            ViewData["Title"] = "New category";
            return View("CategoryForm", new CategoryFormShellViewModel());
        }

        [HttpGet("masters/categories/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult EditCategory(int id)
        {
            ViewData["Title"] = "Edit category";
            return View("CategoryForm", new CategoryFormShellViewModel { Id = id });
        }

        [HttpGet("masters/services")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Services()
        {
            ViewData["Title"] = "Services";
            return View();
        }

        [HttpGet("masters/services/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult NewService()
        {
            ViewData["Title"] = "New service";
            return View("ServiceForm", new ServiceFormShellViewModel());
        }

        [HttpGet("masters/services/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult EditService(int id)
        {
            ViewData["Title"] = "Edit service";
            return View("ServiceForm", new ServiceFormShellViewModel { Id = id });
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
        public IActionResult Menus()
        {
            ViewData["Title"] = "Menu";
            return View("Menus");
        }

        [HttpGet("masters/menu/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult NewMenu()
        {
            ViewData["Title"] = "New menu item";
            return View("MenuForm", new MenuFormShellViewModel());
        }

        [HttpGet("masters/menu/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult EditMenu(int id)
        {
            ViewData["Title"] = "Edit menu item";
            return View("MenuForm", new MenuFormShellViewModel { Id = id });
        }

        [HttpGet("approvals")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Approvals()
        {
            ViewData["Title"] = "Partner approvals";
            return View();
        }

        [HttpGet("users/partners")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Partners()
        {
            ViewData["Title"] = "Partners";
            return View();
        }

        [HttpGet("users/customers")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Customers()
        {
            ViewData["Title"] = "Customers";
            return View("Users", new UsersShellViewModel { Role = UserRoles.Customer, Heading = "Customers" });
        }

        [HttpGet("users/admins")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Admins()
        {
            ViewData["Title"] = "Administrators";
            return View("Users", new UsersShellViewModel
            {
                Role = UserRoles.Admin,
                Heading = "Administrators",
                IncludeSuperAdmins = true,
            });
        }

        [HttpGet("masters/plans")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Plans()
        {
            ViewData["Title"] = "Commission plans";
            return View();
        }

        [HttpGet("masters/plans/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult NewPlan()
        {
            ViewData["Title"] = "New plan";
            return View("PlanForm", new PlanFormShellViewModel());
        }

        [HttpGet("masters/plans/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult EditPlan(int id)
        {
            ViewData["Title"] = "Edit plan";
            return View("PlanForm", new PlanFormShellViewModel { Id = id });
        }

        [HttpGet("masters/taxes")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Taxes()
        {
            ViewData["Title"] = "Tax rules";
            return View();
        }

        [HttpGet("masters/taxes/new")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult NewTax()
        {
            ViewData["Title"] = "New tax rule";
            return View("TaxForm", new TaxFormShellViewModel());
        }

        [HttpGet("masters/taxes/{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult EditTax(int id)
        {
            ViewData["Title"] = "Edit tax rule";
            return View("TaxForm", new TaxFormShellViewModel { Id = id });
        }

        [HttpGet("payouts")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Payouts()
        {
            ViewData["Title"] = "Partner payouts";
            return View();
        }

        [HttpGet("payouts/{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult PartnerLedger(int id)
        {
            ViewData["Title"] = "Partner earnings";
            return View(new PartnerLedgerShellViewModel { PartnerId = id });
        }

        [HttpGet("errors")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public IActionResult Errors()
        {
            ViewData["Title"] = "Error log";
            return View();
        }

        [HttpGet("tasks")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Tasks()
        {
            ViewData["Title"] = "Tasks";
            return View();
        }

        [HttpGet("settings/auth")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<IActionResult> AuthSettings(CancellationToken ct)
        {
            ViewData["Title"] = "Login settings";

            var settings = await _authSettings.GetAsync(ct);

            return View(new AuthSettingsViewModel
            {
                CustomerLoginMode = settings.CustomerLoginMode,
                PartnerLoginMode = settings.PartnerLoginMode,
            });
        }

        [HttpPost("settings/auth")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AuthSettings(AuthSettingsViewModel form, CancellationToken ct)
        {
            if (!LoginMode.IsValid(form.CustomerLoginMode) || !LoginMode.IsValid(form.PartnerLoginMode))
            {
                TempData["Error"] = "Choose a valid login mode for each portal.";
                return Redirect("/admin/settings/auth");
            }

            await _authSettings.UpdateAsync(form.CustomerLoginMode, form.PartnerLoginMode, ct);

            TempData["Success"] = "Login settings saved.";
            return Redirect("/admin/settings/auth");
        }

        [HttpGet("masters/banners")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Banners()
        {
            ViewData["Title"] = "Storefront banners";
            return View();
        }

        [HttpGet("masters/banners/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult NewBanner()
        {
            ViewData["Title"] = "New banner";
            return View("BannerForm", new BannerFormShellViewModel());
        }

        [HttpGet("masters/banners/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult EditBanner(int id)
        {
            ViewData["Title"] = "Edit banner";
            return View("BannerForm", new BannerFormShellViewModel { Id = id });
        }

        [HttpGet("masters/servicezones")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult ServiceZones()
        {
            ViewData["Title"] = "Service zones";
            return View();
        }

        [HttpGet("masters/servicezones/new")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult NewServiceZone()
        {
            ViewData["Title"] = "New service zone";
            return View("ServiceZoneForm", new ServiceZoneFormShellViewModel());
        }

        [HttpGet("masters/servicezones/{id:int}/edit")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult EditServiceZone(int id)
        {
            ViewData["Title"] = "Edit service zone";
            return View("ServiceZoneForm", new ServiceZoneFormShellViewModel { Id = id });
        }

        [HttpGet("enquiries")]
        [Authorize(Policy = Policies.AdminOnly)]
        public IActionResult Enquiries()
        {
            ViewData["Title"] = "Support enquiries";
            return View();
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

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
