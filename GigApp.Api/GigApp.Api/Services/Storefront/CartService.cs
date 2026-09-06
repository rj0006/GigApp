using System.Text.Json;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Storefront
{
    /// <summary>
    /// What a visitor has picked, before they have an account. Only service item
    /// ids and quantities are kept — every price is looked up again on the
    /// server, so a tampered cart cannot change what anything costs.
    /// </summary>
    public interface ICartService
    {
        IReadOnlyList<CartLine> Read();

        void Add(int serviceItemId, int quantity);

        void SetQuantity(int serviceItemId, int quantity);

        void Clear();

        Task<CartViewDto> PriceAsync(CancellationToken ct = default);
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

        public IReadOnlyList<CartLine> Read()
        {
            var raw = Session?.GetString(SessionKey);
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<CartLine>();

            try
            {
                return JsonSerializer.Deserialize<List<CartLine>>(raw) ?? new List<CartLine>();
            }
            catch (JsonException)
            {
                // A cart nobody can read is a cart nobody needs.
                Clear();
                return Array.Empty<CartLine>();
            }
        }

        public void Add(int serviceItemId, int quantity)
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

            Save(lines);
        }

        public void SetQuantity(int serviceItemId, int quantity)
        {
            var lines = Read().ToList();
            var existing = lines.FindIndex(l => l.ServiceItemId == serviceItemId);

            if (existing < 0) return;

            if (quantity <= 0) lines.RemoveAt(existing);
            else lines[existing] = lines[existing] with { Quantity = Math.Min(quantity, MaxQuantity) };

            Save(lines);
        }

        public void Clear() => Session?.Remove(SessionKey);

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
                Save(priced.Select(p => new CartLine(p.ServiceItemId, p.Quantity)).ToList());

            return new CartViewDto { Lines = priced };
        }

        private void Save(List<CartLine> lines)
        {
            if (Session is null) return;

            if (lines.Count == 0) Session.Remove(SessionKey);
            else Session.SetString(SessionKey, JsonSerializer.Serialize(lines));
        }
    }
}
