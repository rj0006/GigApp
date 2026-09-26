using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Storefront;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// The public storefront. Everything up to placing the order works without
    /// an account, because asking a visitor to sign up before they know what
    /// anything costs is how you lose them. [AllowAnonymous] is on every action
    /// individually except PlaceOrder — see CLAUDE.md's Gotchas.
    /// </summary>
    public class ShopController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICartService _cart;
        private readonly ICheckoutService _checkout;
        private readonly IAddressService _addresses;

        public ShopController(
            AppDbContext context,
            ICartService cart,
            ICheckoutService checkout,
            IAddressService addresses)
        {
            _context = context;
            _cart = cart;
            _checkout = checkout;
            _addresses = addresses;
        }

        public override async Task OnActionExecutionAsync(
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
        {
            // The cart badge is in the storefront header on every page.
            ViewData["CartCount"] = _cart.Read().Sum(l => l.Quantity);
            await next();
        }

        [AllowAnonymous]
        [HttpGet("/services")]
        public async Task<IActionResult> Index(string? q, CancellationToken ct)
        {
            var term = q?.Trim();

            if (!string.IsNullOrWhiteSpace(term)) return await SearchAsync(term, ct);

            ViewData["Title"] = "Home services at your doorstep";

            return View(await BuildHomeAsync(ct));
        }

        [AllowAnonymous]
        [HttpGet("/api/shop/home")]
        public async Task<ActionResult<StorefrontViewModel>> HomeJson(CancellationToken ct) =>
            Ok(await BuildHomeAsync(ct));

        [AllowAnonymous]
        [HttpGet("/api/shop/search")]
        public async Task<ActionResult<StorefrontSearchViewModel>> SearchJson(string q, CancellationToken ct) =>
            Ok(await BuildSearchAsync(q.Trim(), ct));

        private async Task<StorefrontViewModel> BuildHomeAsync(CancellationToken ct)
        {
            var categories = await BuildCategoriesAsync(ct);

            var popular = await SellableItems()
                .OrderByDescending(s => s.Tasks.Count(t => t.Status == GigTaskStatus.Completed))
                .ThenBy(s => s.DisplayOrder)
                .Take(8)
                .ToListAsync(ct);

            var fresh = await SellableItems()
                .OrderByDescending(s => s.CreatedAt)
                .Take(6)
                .ToListAsync(ct);

            // One strip per category, so the page reads like a shop rather than
            // one long undifferentiated list.
            var strips = new List<CategoryStripViewModel>();

            foreach (var tile in categories.Take(6))
            {
                var services = await SellableItems()
                    .Where(s => s.SkillCategoryId == tile.Category.Id)
                    .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                    .Take(6)
                    .ToListAsync(ct);

                if (services.Count == 0) continue;

                strips.Add(new CategoryStripViewModel
                {
                    Category = tile.Category,
                    TotalCount = tile.ServiceCount,
                    Services = services.Select(ServiceItemDto.From).ToList(),
                });
            }

            var banners = await LiveBannersAsync(ct);

            return new StorefrontViewModel
            {
                Categories = categories,
                Popular = popular.Select(ServiceItemDto.From).ToList(),
                NewAndNoteworthy = fresh.Select(ServiceItemDto.From).ToList(),
                Strips = strips,
                Spotlight = banners.Where(b => b.Placement == BannerPlacements.Spotlight).ToList(),
                WideBanner = banners.FirstOrDefault(b => b.Placement == BannerPlacements.Wide),
                Stats = await BuildStatsAsync(ct),
                Cart = await _cart.PriceAsync(ct),
            };
        }

        private async Task<IActionResult> SearchAsync(string term, CancellationToken ct)
        {
            ViewData["Title"] = $"Search: {term}";

            return View("Search", await BuildSearchAsync(term, ct));
        }

        private async Task<StorefrontSearchViewModel> BuildSearchAsync(string term, CancellationToken ct)
        {
            var lowered = term.ToLower();

            var matches = await SellableItems()
                .Where(s => s.Name.ToLower().Contains(lowered)
                         || s.SkillCategory!.Name.ToLower().Contains(lowered)
                         || (s.Description != null && s.Description.ToLower().Contains(lowered)))
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .Take(40)
                .ToListAsync(ct);

            return new StorefrontSearchViewModel
            {
                Term = term,
                Results = matches.Select(ServiceItemDto.From).ToList(),
                Categories = await BuildCategoriesAsync(ct),
                Cart = await _cart.PriceAsync(ct),
            };
        }

        private async Task<IReadOnlyList<BannerDto>> LiveBannersAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var banners = await _context.Banners
                .AsNoTracking()
                .Where(b => b.IsActive
                         && b.ImageFileName != null
                         && (b.StartsAt == null || b.StartsAt <= now)
                         && (b.EndsAt == null || b.EndsAt >= now))
                .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
                .ToListAsync(ct);

            return banners.Select(BannerDto.From).ToList();
        }

        private async Task<StorefrontStatsViewModel> BuildStatsAsync(CancellationToken ct)
        {
            var rated = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRoles.Partner && u.RatingCount > 0)
                .Select(u => new { u.AverageRating, u.RatingCount })
                .ToListAsync(ct);

            return new StorefrontStatsViewModel
            {
                AverageRating = rated.Sum(r => r.RatingCount) == 0
                    ? null
                    : Math.Round(
                        rated.Sum(r => r.AverageRating!.Value * r.RatingCount) / rated.Sum(r => r.RatingCount), 1),
                RatingCount = rated.Sum(r => r.RatingCount),
                CompletedCount = await _context.GigTasks
                    .CountAsync(t => t.Status == GigTaskStatus.Completed, ct),
                PartnerCount = await _context.Partners
                    .CountAsync(p => p.KycStatus == KycStatus.Approved, ct),
            };
        }

        [AllowAnonymous]
        [HttpGet("/services/{categoryId:int}")]
        public async Task<IActionResult> Category(int categoryId, CancellationToken ct)
        {
            var vm = await BuildCategoryAsync(categoryId, ct);
            if (vm is null) return Redirect("/services");

            ViewData["Title"] = vm.Category.Name;

            return View(vm);
        }

        [AllowAnonymous]
        [HttpGet("/api/shop/category/{categoryId:int}")]
        public async Task<ActionResult<StorefrontCategoryViewModel>> CategoryJson(int categoryId, CancellationToken ct)
        {
            var vm = await BuildCategoryAsync(categoryId, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        private async Task<StorefrontCategoryViewModel?> BuildCategoryAsync(int categoryId, CancellationToken ct)
        {
            var category = await _context.SkillCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.IsActive, ct);

            if (category is null) return null;

            var items = await SellableItems()
                .Where(s => s.SkillCategoryId == categoryId)
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .ToListAsync(ct);

            return new StorefrontCategoryViewModel
            {
                Category = SkillCategoryDto.From(category),
                Services = items.Select(ServiceItemDto.From).ToList(),
                Categories = await BuildCategoriesAsync(ct),
                Cart = await _cart.PriceAsync(ct),
            };
        }

        [AllowAnonymous]
        [HttpGet("/cart")]
        public async Task<IActionResult> Cart(CancellationToken ct)
        {
            ViewData["Title"] = "Your cart";

            return View(new StorefrontCartViewModel
            {
                Cart = await _cart.PriceAsync(ct),
                Categories = await BuildCategoriesAsync(ct),
            });
        }

        [AllowAnonymous]
        [HttpGet("/checkout")]
        public async Task<IActionResult> Checkout(CancellationToken ct)
        {
            ViewData["Title"] = "Checkout";

            var cart = await _cart.PriceAsync(ct);
            if (cart.IsEmpty) return Redirect("/cart");

            var isCustomer = User.Identity?.IsAuthenticated == true
                             && User.IsInRole(UserRoles.Customer);

            var userId = isCustomer ? User.GetUserId() : null;

            return View(new StorefrontCheckoutViewModel
            {
                Cart = cart,
                IsSignedIn = isCustomer,
                Addresses = userId is null
                    ? Array.Empty<AddressDto>()
                    : await _addresses.ListAsync(userId.Value, ct),
            });
        }

        [HttpPost("/checkout")]
        [Authorize(Policy = Policies.CustomerOnly)]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken ct)
        {
            var result = await _checkout.PlaceAsync(
                User.GetRequiredUserId(), request.AddressId, request.Note, request.PreferredAt.ToUtc(), ct);

            if (!result.Succeeded)
                return BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });

            TempData["Success"] = result.TaskIds.Count == 1
                ? "Booked. We are finding you the nearest partner now."
                : $"{result.TaskIds.Count} services booked. We are finding partners for each now.";

            return Ok(new { redirectTo = "/customer/profile/tasks" });
        }

        private IQueryable<ServiceItem> SellableItems() =>
            _context.ServiceItems
                .AsNoTracking()
                .Include(s => s.SkillCategory)
                .Where(s => s.IsActive
                         && s.SkillCategory!.IsActive
                         && s.AllowsInstantBooking
                         && s.BasePayout > 0);

        private async Task<IReadOnlyList<CatalogCategoryViewModel>> BuildCategoriesAsync(
            CancellationToken ct) =>
            await _context.SkillCategories
                .AsNoTracking()
                .Where(c => c.IsActive
                         && c.ServiceItems.Any(s => s.IsActive && s.AllowsInstantBooking && s.BasePayout > 0))
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new CatalogCategoryViewModel
                {
                    Category = SkillCategoryDto.From(c),
                    ServiceCount = c.ServiceItems.Count(
                        s => s.IsActive && s.AllowsInstantBooking && s.BasePayout > 0),
                    StartingFrom = c.ServiceItems
                        .Where(s => s.IsActive && s.AllowsInstantBooking && s.BasePayout > 0)
                        .Min(s => s.BasePayout),
                })
                .ToListAsync(ct);
    }
}
