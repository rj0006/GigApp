using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Pricing;
using GigApp.Api.Services.Addresses;
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

        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;
        private readonly IPriceInsightService _priceInsights;
        private readonly IUserAdminService _userAdmin;

        public AdminController(
            IAuthService authService,
            IProfileService profileService,
            IAddressService addressService,
            AppDbContext context,
            ICategoryLookup categories,
            IPriceInsightService priceInsights,
            IUserAdminService userAdmin)
            : base(authService, profileService, addressService)
        {
            _context = context;
            _categories = categories;
            _priceInsights = priceInsights;
            _userAdmin = userAdmin;
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

                // The sidebar hides the super-admin section for everyone else.
                ViewData["IsSuperAdmin"] = User.IsSuperAdmin();
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

            _context.SkillCategories.Add(new SkillCategory
            {
                Name = name,
                Description = Normalize(form.Description),
                IsActive = form.IsActive,
                DisplayOrder = form.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
            });

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
            });
        }

        [HttpPost("approvals/{id:int}/verify")]
        [Authorize(Policy = Policies.AdminOnly)]
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
            TempData["Success"] =
                $"Password reset for {result.User!.Name}. New password: {result.GeneratedPassword} "
              + "— copy it now, it will not be shown again.";

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

        private IQueryable<Partner> PartnersWithDetail =>
            _context.Partners.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory);

        private IQueryable<GigTask> TasksWithDetail =>
            _context.GigTasks.AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.ServiceItem)
                .Include(t => t.Customer)
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
