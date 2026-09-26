using System.Text.Json;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Storefront
{
    /// <summary>
    /// What a visitor has picked. Only service item ids and quantities are kept
    /// — every price is looked up again on the server, so a tampered cart cannot
    /// change what anything costs. A guest's cart lives in session only; once
    /// signed in as a customer, every change is also mirrored to
    /// <see cref="CartItem"/> so it survives past that session.
    /// </summary>
    public interface ICartService
    {
        IReadOnlyList<CartLine> Read();

        Task AddAsync(int serviceItemId, int quantity, CancellationToken ct = default);

        Task SetQuantityAsync(int serviceItemId, int quantity, CancellationToken ct = default);

        Task ClearAsync(CancellationToken ct = default);

        Task<CartViewDto> PriceAsync(CancellationToken ct = default);

        Task MergeIntoAccountAsync(int customerId, CancellationToken ct = default);
    }

    public record CartLine(int ServiceItemId, int Quantity);

    public class CartService : ICartService
    {
        public const string SessionKey = "gigapp_cart";
        public const int MaxQuantity = 20;
        public const int MaxLines = 20;

        private readonly IHttpContextAccessor _http;
        private readonly AppDbContext _context;

        public CartService(IHttpContextAccessor http, AppDbContext context)
        {
            _http = http;
            _context = context;
        }

        private ISession? Session => _http.HttpContext?.Session;

        private int? CurrentCustomerId()
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true || !user.IsInRole(UserRoles.Customer))
                return null;

            return user.GetUserId();
        }

        public IReadOnlyList<CartLine> Read()
        {
            // A signed-in customer's cart is the DB row, not the session — mobile carries no session cookie.
            var customerId = CurrentCustomerId();
            if (customerId is not null)
            {
                return _context.CartItems
                    .AsNoTracking()
                    .Where(c => c.CustomerId == customerId)
                    .OrderBy(c => c.Id)
                    .Select(c => new CartLine(c.ServiceItemId, c.Quantity))
                    .ToList();
            }

            var raw = Session?.GetString(SessionKey);
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<CartLine>();

            try
            {
                return JsonSerializer.Deserialize<List<CartLine>>(raw) ?? new List<CartLine>();
            }
            catch (JsonException)
            {
                // A cart nobody can read is a cart nobody needs.
                Session?.Remove(SessionKey);
                return Array.Empty<CartLine>();
            }
        }

        public async Task AddAsync(int serviceItemId, int quantity, CancellationToken ct = default)
        {
            if (serviceItemId <= 0) return;

            var lines = Read().ToList();
            var existing = lines.FindIndex(l => l.ServiceItemId == serviceItemId);

            if (existing >= 0)
            {
                var wanted = lines[existing].Quantity + Math.Max(1, quantity);
                lines[existing] = lines[existing] with { Quantity = Math.Min(wanted, MaxQuantity) };
            }
            else
            {
                if (lines.Count >= MaxLines) return;
                lines.Add(new CartLine(serviceItemId, Math.Clamp(quantity, 1, MaxQuantity)));
            }

            await SaveAsync(lines, ct);
        }

        public async Task SetQuantityAsync(
            int serviceItemId, int quantity, CancellationToken ct = default)
        {
            var lines = Read().ToList();
            var existing = lines.FindIndex(l => l.ServiceItemId == serviceItemId);

            if (existing < 0) return;

            if (quantity <= 0) lines.RemoveAt(existing);
            else lines[existing] = lines[existing] with { Quantity = Math.Min(quantity, MaxQuantity) };

            await SaveAsync(lines, ct);
        }

        public async Task ClearAsync(CancellationToken ct = default) => await SaveAsync(new List<CartLine>(), ct);

        public async Task MergeIntoAccountAsync(int customerId, CancellationToken ct = default)
        {
            var sessionLines = Read().ToList();

            var stored = await _context.CartItems
                .Where(c => c.CustomerId == customerId)
                .ToListAsync(ct);

            var merged = stored.Select(s => new CartLine(s.ServiceItemId, s.Quantity)).ToList();

            foreach (var line in sessionLines)
            {
                var existing = merged.FindIndex(l => l.ServiceItemId == line.ServiceItemId);

                if (existing >= 0)
                {
                    var wanted = merged[existing].Quantity + line.Quantity;
                    merged[existing] = merged[existing] with { Quantity = Math.Min(wanted, MaxQuantity) };
                }
                else if (merged.Count < MaxLines)
                {
                    merged.Add(line with { Quantity = Math.Min(line.Quantity, MaxQuantity) });
                }
            }

            if (Session is not null)
            {
                if (merged.Count == 0) Session.Remove(SessionKey);
                else Session.SetString(SessionKey, JsonSerializer.Serialize(merged));
            }

            _context.CartItems.RemoveRange(stored);
            _context.CartItems.AddRange(merged.Select(l => new CartItem
            {
                CustomerId = customerId,
                ServiceItemId = l.ServiceItemId,
                Quantity = l.Quantity,
                UpdatedAt = DateTime.UtcNow,
            }));

            await _context.SaveChangesAsync(ct);
        }

        public async Task<CartViewDto> PriceAsync(CancellationToken ct = default)
        {
            var lines = Read();
            if (lines.Count == 0) return new CartViewDto();

            var ids = lines.Select(l => l.ServiceItemId).ToList();

            var items = await _context.ServiceItems
                .AsNoTracking()
                .Include(s => s.SkillCategory)
                .Where(s => ids.Contains(s.Id) && s.IsActive)
                .ToListAsync(ct);

            var priced = new List<CartLineDto>();

            foreach (var line in lines)
            {
                var item = items.FirstOrDefault(i => i.Id == line.ServiceItemId);

                // Deactivated or deleted since it was added. Drop it rather than
                // letting a checkout fail on something the visitor cannot see.
                if (item is null || item.BasePayout is not > 0) continue;

                priced.Add(new CartLineDto
                {
                    ServiceItemId = item.Id,
                    ServiceItemName = item.Name,
                    CategoryId = item.SkillCategoryId,
                    CategoryName = item.SkillCategory?.Name ?? string.Empty,
                    ImageFileName = item.ImageFileName,
                    UnitPrice = item.BasePayout.Value,
                    Quantity = line.Quantity,
                    AllowsInstantBooking = item.AllowsInstantBooking,
                });
            }

            if (priced.Count != lines.Count)
                await SaveAsync(priced.Select(p => new CartLine(p.ServiceItemId, p.Quantity)).ToList(), ct);

            return new CartViewDto { Lines = priced };
        }

        private async Task SaveAsync(List<CartLine> lines, CancellationToken ct)
        {
            if (Session is not null)
            {
                if (lines.Count == 0) Session.Remove(SessionKey);
                else Session.SetString(SessionKey, JsonSerializer.Serialize(lines));
            }

            var customerId = CurrentCustomerId();
            if (customerId is null) return;

            var existing = await _context.CartItems
                .Where(c => c.CustomerId == customerId)
                .ToListAsync(ct);

            _context.CartItems.RemoveRange(existing);
            _context.CartItems.AddRange(lines.Select(l => new CartItem
            {
                CustomerId = customerId.Value,
                ServiceItemId = l.ServiceItemId,
                Quantity = l.Quantity,
                UpdatedAt = DateTime.UtcNow,
            }));

            await _context.SaveChangesAsync(ct);
        }
    }
}
