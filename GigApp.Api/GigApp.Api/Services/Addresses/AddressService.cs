using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Addresses
{
    /// <summary>
    /// A user's saved addresses. All rules live here so the API and the portals
    /// behave identically — the mobile apps get the same behaviour for free.
    /// </summary>
    public interface IAddressService
    {
        Task<IReadOnlyList<AddressDto>> ListAsync(int userId, CancellationToken ct = default);
        Task<AddressDto?> GetAsync(int userId, int addressId, CancellationToken ct = default);
        Task<AddressResult> CreateAsync(int userId, SaveAddressRequest request, CancellationToken ct = default);
        Task<AddressResult> UpdateAsync(int userId, int addressId, SaveAddressRequest request, CancellationToken ct = default);
        Task<AddressResult> SetDefaultAsync(int userId, int addressId, CancellationToken ct = default);
        Task<AddressResult> DeleteAsync(int userId, int addressId, CancellationToken ct = default);

        /// <summary>The address entity, only if this user owns it and it is live.</summary>
        Task<Address?> FindOwnedAsync(int userId, int addressId, CancellationToken ct = default);
    }

    public class AddressService : IAddressService
    {
        private readonly AppDbContext _context;

        public AddressService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<AddressDto>> ListAsync(int userId, CancellationToken ct = default)
        {
            var addresses = await Live(userId)
                .AsNoTracking()
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.Label)
                .ToListAsync(ct);

            return addresses.Select(AddressDto.From).ToList();
        }

        public async Task<AddressDto?> GetAsync(int userId, int addressId, CancellationToken ct = default)
        {
            var address = await Live(userId).AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == addressId, ct);

            return address is null ? null : AddressDto.From(address);
        }

        public Task<Address?> FindOwnedAsync(int userId, int addressId, CancellationToken ct = default) =>
            Live(userId).FirstOrDefaultAsync(a => a.Id == addressId, ct);

        public async Task<AddressResult> CreateAsync(
            int userId, SaveAddressRequest request, CancellationToken ct = default)
        {
            var error = Validate(request);
            if (error is not null) return AddressResult.Fail(error);

            var address = new Address
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
            };

            Apply(address, request);

            // The very first address is the default whether or not it was asked
            // for — otherwise the picker opens with nothing selected.
            var isFirst = !await Live(userId).AnyAsync(ct);
            address.IsDefault = request.IsDefault || isFirst;

            _context.Addresses.Add(address);

            if (address.IsDefault) await ClearOtherDefaultsAsync(userId, exceptId: null, ct);

            await _context.SaveChangesAsync(ct);

            return AddressResult.Ok(AddressDto.From(address));
        }

        public async Task<AddressResult> UpdateAsync(
            int userId, int addressId, SaveAddressRequest request, CancellationToken ct = default)
        {
            var error = Validate(request);
            if (error is not null) return AddressResult.Fail(error);

            var address = await FindOwnedAsync(userId, addressId, ct);
            if (address is null) return AddressResult.Fail("Address not found.");

            Apply(address, request);
            address.UpdatedAt = DateTime.UtcNow;

            if (request.IsDefault && !address.IsDefault)
            {
                await ClearOtherDefaultsAsync(userId, exceptId: addressId, ct);
                address.IsDefault = true;
            }

            await _context.SaveChangesAsync(ct);

            return AddressResult.Ok(AddressDto.From(address));
        }

        public async Task<AddressResult> SetDefaultAsync(
            int userId, int addressId, CancellationToken ct = default)
        {
            var address = await FindOwnedAsync(userId, addressId, ct);
            if (address is null) return AddressResult.Fail("Address not found.");

            await ClearOtherDefaultsAsync(userId, exceptId: addressId, ct);

            address.IsDefault = true;
            address.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return AddressResult.Ok(AddressDto.From(address));
        }

        public async Task<AddressResult> DeleteAsync(
            int userId, int addressId, CancellationToken ct = default)
        {
            var address = await FindOwnedAsync(userId, addressId, ct);
            if (address is null) return AddressResult.Fail("Address not found.");

            // Soft delete: past tasks point here, and hard-deleting would lose
            // where that work actually happened.
            address.IsDeleted = true;
            address.IsDefault = false;
            address.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            // Never leave a user without a default while they still have addresses.
            var replacement = await Live(userId).FirstOrDefaultAsync(ct);
            if (replacement is not null && !await Live(userId).AnyAsync(a => a.IsDefault, ct))
            {
                replacement.IsDefault = true;
                await _context.SaveChangesAsync(ct);
            }

            return AddressResult.Ok(AddressDto.From(address));
        }

        // ----------------------------------------------------------- helpers

        private IQueryable<Address> Live(int userId) =>
            _context.Addresses.Where(a => a.UserId == userId && !a.IsDeleted);

        private async Task ClearOtherDefaultsAsync(int userId, int? exceptId, CancellationToken ct)
        {
            var others = await _context.Addresses
                .Where(a => a.UserId == userId && a.IsDefault && (exceptId == null || a.Id != exceptId))
                .ToListAsync(ct);

            foreach (var other in others) other.IsDefault = false;
        }

        private static string? Validate(SaveAddressRequest request)
        {
            if (!AddressLabel.IsValid(request.Label))
                return "Choose a valid label.";

            // Half a pin is not a location, and silently keeping one half would
            // make an address look mappable when it is not.
            var hasLat = request.Latitude is not null;
            var hasLon = request.Longitude is not null;

            if (hasLat != hasLon)
                return "Latitude and longitude must be provided together.";

            return null;
        }

        private static void Apply(Address address, SaveAddressRequest request)
        {
            address.Label = request.Label;
            address.HouseNumber = Trim(request.HouseNumber);
            address.Line1 = request.Line1.Trim();
            address.Line2 = Trim(request.Line2);
            address.Landmark = Trim(request.Landmark);
            address.City = request.City.Trim();
            address.State = Trim(request.State);
            address.Pincode = request.Pincode.Trim();
            address.Latitude = request.Latitude;
            address.Longitude = request.Longitude;
        }

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
