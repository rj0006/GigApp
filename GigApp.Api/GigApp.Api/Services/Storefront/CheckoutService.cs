using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Booking;
using GigApp.Api.Services.Geo;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Storefront
{
    public record CheckoutResult(bool Succeeded, string? Error, IReadOnlyList<int> TaskIds)
    {
        public static CheckoutResult Ok(IReadOnlyList<int> taskIds) => new(true, null, taskIds);

        public static CheckoutResult Fail(string error) =>
            new(false, error, Array.Empty<int>());
    }

    /// <summary>
    /// Turns a priced cart into bookings. One task per line, because a line is
    /// one service at one address and that is what a partner is sent to do; the
    /// quantity rides along so a three-unit job stays one visit.
    /// </summary>
    public interface ICheckoutService
    {
        Task<CheckoutResult> PlaceAsync(
            int customerId, int addressId, string? note, DateTime? preferredAt,
            CancellationToken ct = default);
    }

    public class CheckoutService : ICheckoutService
    {
        private readonly AppDbContext _context;
        private readonly ICartService _cart;
        private readonly IAddressService _addresses;
        private readonly IOfferService _offers;
        private readonly ILogger<CheckoutService> _logger;

        public CheckoutService(
            AppDbContext context,
            ICartService cart,
            IAddressService addresses,
            IOfferService offers,
            ILogger<CheckoutService> logger)
        {
            _context = context;
            _cart = cart;
            _addresses = addresses;
            _offers = offers;
            _logger = logger;
        }

        public async Task<CheckoutResult> PlaceAsync(
            int customerId, int addressId, string? note, DateTime? preferredAt,
            CancellationToken ct = default)
        {
            var cart = await _cart.PriceAsync(ct);

            if (cart.IsEmpty) return CheckoutResult.Fail("Your cart is empty.");

            var address = await _addresses.FindOwnedAsync(customerId, addressId, ct);
            if (address is null) return CheckoutResult.Fail("Choose one of your saved addresses.");

            var line = address.ToSingleLine();
            var point = GeoPoint.From(address.Latitude, address.Longitude);
            var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            var tasks = cart.Lines.Select(item => new GigTask
            {
                CustomerId = customerId,
                CategoryId = item.CategoryId,
                ServiceItemId = item.ServiceItemId,
                Quantity = item.Quantity,
                Urgency = TaskUrgency.Normal,
                Description = Describe(item, trimmed),

                AddressId = address.Id,
                Address = line,
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                Location = point,

                // A catalogue line is always a fixed price — that is what the
                // storefront showed, so it is what the booking has to be.
                Budget = item.LineTotal,
                AgreedAmount = item.LineTotal,
                BookingMode = TaskBookingMode.Instant,

                PreferredDateTime = preferredAt,
                Status = GigTaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            }).ToList();

            _context.GigTasks.AddRange(tasks);
            await _context.SaveChangesAsync(ct);

            await _cart.ClearAsync(ct);

            // Each line looks for its own partner — they are different trades.
            foreach (var task in tasks) await _offers.StartAsync(task.Id, ct);

            _logger.LogInformation(
                "Customer {CustomerId} checked out {Count} line(s) for {Total}",
                customerId, tasks.Count, cart.Total);

            return CheckoutResult.Ok(tasks.Select(t => t.Id).ToList());
        }

        private static string Describe(CartLineDto item, string? note)
        {
            var quantity = item.Quantity > 1 ? $" x {item.Quantity}" : string.Empty;
            var text = $"{item.ServiceItemName}{quantity}";

            return note is null ? text : $"{text}. {note}";
        }
    }
}
