using System.Security.Cryptography;
using System.Text;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GigApp.Api.Configuration;

namespace GigApp.Api.Services
{
    public record RefreshTokenResult(bool Succeeded, string? Error, AuthResponse? Response)
    {
        public static RefreshTokenResult Ok(AuthResponse response) => new(true, null, response);
        public static RefreshTokenResult Fail(string error) => new(false, error, null);
    }

    public interface IRefreshTokenService
    {
        Task<(string RawToken, DateTime ExpiresAtUtc)> IssueAsync(
            int userId, string? deviceLabel, string? ipAddress, CancellationToken ct = default);

        Task<RefreshTokenResult> RedeemAsync(
            string rawToken, string? deviceLabel, string? ipAddress, CancellationToken ct = default);

        Task RevokeByRawTokenAsync(string? rawToken, CancellationToken ct = default);

        Task<IReadOnlyList<DeviceSessionDto>> ListActiveAsync(
            int userId, string? currentRawToken, CancellationToken ct = default);

        Task<bool> RevokeAsync(int userId, int refreshTokenId, CancellationToken ct = default);

        Task RevokeAllExceptAsync(int userId, string? currentRawToken, CancellationToken ct = default);
    }

    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly JwtSettings _settings;

        public RefreshTokenService(
            AppDbContext context, ITokenService tokenService, IOptions<JwtSettings> settings)
        {
            _context = context;
            _tokenService = tokenService;
            _settings = settings.Value;
        }

        public async Task<(string RawToken, DateTime ExpiresAtUtc)> IssueAsync(
            int userId, string? deviceLabel, string? ipAddress, CancellationToken ct = default)
        {
            var raw = GenerateRawToken();
            var expiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpiryDays);

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = Hash(raw),
                DeviceLabel = deviceLabel,
                IpAddress = ipAddress,
                ExpiresAt = expiresAt,
            });

            await _context.SaveChangesAsync(ct);

            return (raw, expiresAt);
        }

        public async Task<RefreshTokenResult> RedeemAsync(
            string rawToken, string? deviceLabel, string? ipAddress, CancellationToken ct = default)
        {
            var hash = Hash(rawToken);

            var existing = await _context.RefreshTokens
                .Include(r => r.User).ThenInclude(u => u!.PartnerProfile)
                .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

            if (existing is null || !existing.IsActive || existing.User is null)
                return RefreshTokenResult.Fail("Session expired. Please sign in again.");

            if (!existing.User.IsActive)
                return RefreshTokenResult.Fail("This account has been deactivated. Contact support.");

            // Rotation: this exact token can never be redeemed a second time,
            // so a stolen-and-replayed refresh token is caught the moment the
            // real owner's device redeems its own copy first.
            existing.RevokedAt = DateTime.UtcNow;
            existing.LastUsedAt = DateTime.UtcNow;

            var (raw, expiresAt) = await IssueAsync(existing.UserId, deviceLabel ?? existing.DeviceLabel, ipAddress, ct);
            var (jwt, jwtExpiresAt) = _tokenService.CreateToken(existing.User);

            await _context.SaveChangesAsync(ct);

            return RefreshTokenResult.Ok(new AuthResponse
            {
                Token = jwt,
                ExpiresAtUtc = jwtExpiresAt,
                RefreshToken = raw,
                RefreshExpiresAtUtc = expiresAt,
                User = UserDto.From(existing.User),
            });
        }

        public async Task RevokeByRawTokenAsync(string? rawToken, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(rawToken)) return;

            var hash = Hash(rawToken);

            await _context.RefreshTokens
                .Where(r => r.TokenHash == hash && r.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);
        }

        public async Task<IReadOnlyList<DeviceSessionDto>> ListActiveAsync(
            int userId, string? currentRawToken, CancellationToken ct = default)
        {
            var currentHash = string.IsNullOrEmpty(currentRawToken) ? null : Hash(currentRawToken);
            var now = DateTime.UtcNow;

            var rows = await _context.RefreshTokens
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > now)
                .OrderByDescending(r => r.LastUsedAt ?? r.CreatedAt)
                .ToListAsync(ct);

            return rows.Select(r => new DeviceSessionDto
            {
                Id = r.Id,
                DeviceLabel = r.DeviceLabel ?? "Unknown device",
                IpAddress = r.IpAddress,
                CreatedAt = r.CreatedAt,
                LastUsedAt = r.LastUsedAt,
                IsCurrent = currentHash is not null && r.TokenHash == currentHash,
            }).ToList();
        }

        public async Task<bool> RevokeAsync(int userId, int refreshTokenId, CancellationToken ct = default)
        {
            var rows = await _context.RefreshTokens
                .Where(r => r.Id == refreshTokenId && r.UserId == userId && r.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);

            return rows > 0;
        }

        public async Task RevokeAllExceptAsync(
            int userId, string? currentRawToken, CancellationToken ct = default)
        {
            var currentHash = string.IsNullOrEmpty(currentRawToken) ? null : Hash(currentRawToken);

            await _context.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt == null
                         && (currentHash == null || r.TokenHash != currentHash))
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);
        }

        private static string GenerateRawToken() =>
            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        private static string Hash(string raw) =>
            Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
