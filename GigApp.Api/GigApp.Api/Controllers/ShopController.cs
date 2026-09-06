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
    /// anything costs is how you lose them.
    /// </summary>
    [AllowAnonymous]
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

        [HttpGet("/services")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewData["Title"] = "Home services at your doorstep";

            var categories = await BuildCategoriesAsync(ct);

            var popular = await SellableItems()
                .OrderByDescending(s => s.Tasks.Count(t => t.Status == GigTaskStatus.Completed))
                .ThenBy(s => s.DisplayOrder)
                .Take(8)
                .ToListAsync(ct);

            return View(new StorefrontViewModel
            {
                Categories = categories,
                Popular = popular.Select(ServiceItemDto.From).ToList(),
                Cart = await _cart.PriceAsync(ct),
            });
        }

        [HttpGet("/services/{categoryId:int}")]
        public async Task<IActionResult> Category(int categoryId, CancellationToken ct)
        {
            var category = await _context.SkillCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.IsActive, ct);

            if (category is null) return Redirect("/services");

            ViewData["Title"] = category.Name;

            var items = await SellableItems()
                .Where(s => s.SkillCategoryId == categoryId)
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .ToListAsync(ct);

            return View(new StorefrontCategoryViewModel
            {
                Category = SkillCategoryDto.From(category),
                Services = items.Select(ServiceItemDto.From).ToList(),
                Categories = await BuildCategoriesAsync(ct),
                Cart = await _cart.PriceAsync(ct),
            });
        }

        [HttpPost("/cart/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(
            int serviceItemId, int quantity, string? returnTo, CancellationToken ct)
        {
            var sellable = await SellableItems()
                .AnyAsync(s => s.Id == serviceItemId, ct);

            if (!sellable)
            {
                TempData["Error"] = "That service is not available right now.";
                return RedirectBack(returnTo);
            }

            _cart.Add(serviceItemId, quantity < 1 ? 1 : quantity);
            TempData["Success"] = "Added to your cart.";

            return RedirectBack(returnTo);
        }

        [HttpPost("/cart/update")]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCart(int serviceItemId, int quantity, string? returnTo)
        {
            _cart.SetQuantity(serviceItemId, quantity);
            return RedirectBack(returnTo ?? "/cart");
        }

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
            int addressId, string? note, DateTime? preferredAt, CancellationToken ct)
        {
            var result = await _checkout.PlaceAsync(
                User.GetRequiredUserId(), addressId, note, preferredAt.ToUtc(), ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return Redirect("/checkout");
            }

            TempData["Success"] = result.TaskIds.Count == 1
                ? "Booked. We are finding you the nearest partner now."
                : $"{result.TaskIds.Count} services booked. We are finding partners for each now.";

            return Redirect("/customer/profile/tasks");
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

        // Only ever back to a page of our own — a returnTo from the query string
        // is attacker-controlled.
        private IActionResult RedirectBack(string? returnTo) =>
            Redirect(Url.IsLocalUrl(returnTo) ? returnTo! : "/services");
    }
}
