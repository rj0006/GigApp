using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Tracking;
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
        private const string ApprovalsPath = "/admin/approvals";

        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;

        public AdminController(
            IAuthService authService,
            IProfileService profileService,
            AppDbContext context,
            ICategoryLookup categories)
            : base(authService, profileService)
        {
            _context = context;
            _categories = categories;
        }

        protected override string PortalSlug => "admin";
        protected override string RequiredRole => UserRoles.Admin;

        /// <summary>
        /// The sidebar shows a pending-KYC badge on every page, so the count is
        /// resolved once here rather than in each action.
        /// </summary>
        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (User.IsInRole(UserRoles.Admin))
            {
                ViewData["PendingKycCount"] =
                    await _context.Partners.CountAsync(p => !p.IsVerified, context.HttpContext.RequestAborted);
            }

            await next();
        }

        // ------------------------------------------------------------------ auth

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

        // ------------------------------------------------------------- dashboard

        [HttpGet("")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewData["Title"] = "Dashboard";

            var pending = await PartnersWithDetail
                .Where(p => !p.IsVerified)
                .OrderByDescending(p => p.CreatedAt)
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
                PendingKycCount = await _context.Partners.CountAsync(p => !p.IsVerified, ct),
                OpenTaskCount = await _context.GigTasks.CountAsync(t => t.Status == GigTaskStatus.Pending, ct),
                CategoryCount = await _context.SkillCategories.CountAsync(c => c.IsActive, ct),
                PendingPartners = pending.Select(PartnerDto.From).ToList(),
                RecentTasks = recentTasks.Select(GigTaskDto.From).ToList(),
            });
        }

        // ------------------------------------------------- masters: categories

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

        // -------------------------------------------------------- approvals

        [HttpGet("approvals")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Approvals([FromQuery] PageRequest paging, CancellationToken ct)
        {
            ViewData["Title"] = "Partner approvals";

            var page = await PartnersWithDetail
                .Where(p => !p.IsVerified)
                .OrderByDescending(p => p.CreatedAt)
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
                partner.IsVerified = isVerified;
                await _context.SaveChangesAsync(ct);

                // `reason` is picked up as the audit Remark by TrackingActionFilter.
                TrackDoc(partner.Id, PartnerDto.From(partner));
                TempData["Success"] = isVerified
                    ? $"{partner.User?.Name} is now verified."
                    : $"{partner.User?.Name} was rejected.";
            }

            return Redirect(target);
        }

        // -------------------------------------------------- user management

        [HttpGet("users/partners")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Partners(
            [FromQuery] PageRequest paging, bool? verified, int? categoryId, CancellationToken ct = default)
        {
            ViewData["Title"] = "Partners";

            var query = PartnersWithDetail;

            if (verified is not null) query = query.Where(p => p.IsVerified == verified);
            if (categoryId is not null) query = query.Where(p => p.SkillCategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(p =>
                    p.User!.Name.ToLower().Contains(term) || p.User.Phone.Contains(term));
            }

            var page = await query
                .OrderBy(p => p.IsVerified).ThenByDescending(p => p.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return View(new AdminPartnersViewModel
            {
                Partners = page.Map(PartnerDto.From),
                Categories = await _categories.GetActiveOptionsAsync(ct),
                VerifiedFilter = verified,
                CategoryFilter = categoryId,
            });
        }

        [HttpGet("users/customers")]
        [Authorize(Policy = Policies.AdminOnly)]
        public Task<IActionResult> Customers([FromQuery] PageRequest paging, CancellationToken ct) =>
            UserListAsync(UserRoles.Customer, "Customers", paging, ct);

        [HttpGet("users/admins")]
        [Authorize(Policy = Policies.AdminOnly)]
        public Task<IActionResult> Admins([FromQuery] PageRequest paging, CancellationToken ct) =>
            UserListAsync(UserRoles.Admin, "Administrators", paging, ct);

        // ------------------------------------------------------- operations

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

        // ---------------------------------------------------------- helpers

        private IQueryable<Partner> PartnersWithDetail =>
            _context.Partners.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory);

        private IQueryable<GigTask> TasksWithDetail =>
            _context.GigTasks.AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.Customer)
                .Include(t => t.Partner)!.ThenInclude(p => p!.User);

        private async Task<IActionResult> UserListAsync(
            string role, string heading, PageRequest paging, CancellationToken ct)
        {
            ViewData["Title"] = heading;

            var query = _context.Users.AsNoTracking()
                .Include(u => u.PartnerProfile)!.ThenInclude(p => p!.SkillCategory)
                .Where(u => u.Role == role);

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

        private Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken ct) =>
            _context.SkillCategories.AnyAsync(
                c => c.Name.ToLower() == name.ToLower() && (excludingId == null || c.Id != excludingId), ct);

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
