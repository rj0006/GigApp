using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services
{
    public interface IAuthSettingsService
    {
        Task<AuthSettingsDto> GetAsync(CancellationToken ct = default);

        Task UpdateAsync(string customerLoginMode, string partnerLoginMode, CancellationToken ct = default);
    }

    public class AuthSettingsService : IAuthSettingsService
    {
        private readonly AppDbContext _context;

        public AuthSettingsService(AppDbContext context) => _context = context;

        public async Task<AuthSettingsDto> GetAsync(CancellationToken ct = default)
        {
            var settings = await _context.AuthSettings.AsNoTracking().FirstOrDefaultAsync(ct);

            return new AuthSettingsDto
            {
                CustomerLoginMode = settings?.CustomerLoginMode ?? LoginMode.Otp,
                PartnerLoginMode = settings?.PartnerLoginMode ?? LoginMode.Otp,
            };
        }

        public async Task UpdateAsync(
            string customerLoginMode, string partnerLoginMode, CancellationToken ct = default)
        {
            var settings = await _context.AuthSettings.FirstOrDefaultAsync(ct);

            if (settings is null)
            {
                settings = new AuthSettings();
                _context.AuthSettings.Add(settings);
            }

            settings.CustomerLoginMode = customerLoginMode;
            settings.PartnerLoginMode = partnerLoginMode;
            settings.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }
    }
}
