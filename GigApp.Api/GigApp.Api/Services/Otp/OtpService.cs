using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Otp
{
    public record OtpRequestResult(bool Succeeded, string? Error, string? DevCode)
    {
        public static OtpRequestResult Ok(string? devCode) => new(true, null, devCode);
        public static OtpRequestResult Fail(string error) => new(false, error, null);
    }

    public record OtpVerifyResult(bool Succeeded, string? Error, AuthResult? Auth, bool RequiresPartnerRegistration)
    {
        public static OtpVerifyResult Ok(AuthResult auth) => new(true, null, auth, false);
        public static OtpVerifyResult RegisterPartner() => new(true, null, null, true);
        public static OtpVerifyResult Fail(string error) => new(false, error, null, false);
    }

    public interface IOtpService
    {
        Task<OtpRequestResult> RequestAsync(string phone, string role, CancellationToken ct = default);

        Task<OtpVerifyResult> VerifyAsync(
            string phone, string role, string code, string? name, CancellationToken ct = default);
    }

    public class OtpService : IOtpService
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _auth;
        private readonly IOtpSender _sender;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<OtpService> _logger;

        public OtpService(
            AppDbContext context,
            IAuthService auth,
            IOtpSender sender,
            IWebHostEnvironment environment,
            ILogger<OtpService> logger)
        {
            _context = context;
            _auth = auth;
            _sender = sender;
            _environment = environment;
            _logger = logger;
        }

        public async Task<OtpRequestResult> RequestAsync(
            string phone, string role, CancellationToken ct = default)
        {
            phone = phone.Trim();

            var recent = await _context.OtpChallenges
                .Where(o => o.Phone == phone && o.Role == role && o.ConsumedAt == null)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (recent is not null && recent.CreatedAt > DateTime.UtcNow.AddSeconds(-30))
                return OtpRequestResult.Fail(
                    "An OTP was just sent. Wait a few seconds before asking for another.");

            var code = Random.Shared.Next(100000, 999999).ToString();

            _context.OtpChallenges.Add(new OtpChallenge
            {
                Phone = phone,
                Role = role,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpChallenge.ValidityMinutes),
                CreatedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync(ct);
            await _sender.SendAsync(phone, code, ct);

            _logger.LogInformation("OTP requested for {Phone} ({Role})", phone, role);

            return OtpRequestResult.Ok(_environment.IsDevelopment() ? code : null);
        }

        public async Task<OtpVerifyResult> VerifyAsync(
            string phone, string role, string code, string? name, CancellationToken ct = default)
        {
            phone = phone.Trim();
            var now = DateTime.UtcNow;

            var challenge = await _context.OtpChallenges
                .Where(o => o.Phone == phone && o.Role == role)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (challenge is null || !challenge.IsLive(now))
                return OtpVerifyResult.Fail("That code has expired. Request a new one.");

            if (!BCrypt.Net.BCrypt.Verify(code, challenge.CodeHash))
            {
                challenge.Attempts++;
                await _context.SaveChangesAsync(ct);
                return OtpVerifyResult.Fail("That code is not right.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == phone && u.Role == role, ct);

            if (user is not null)
            {
                challenge.ConsumedAt = now;
                await _context.SaveChangesAsync(ct);

                return OtpVerifyResult.Ok(await _auth.LoginWithOtpAsync(phone, role, ct));
            }

            if (role == UserRoles.Partner)
            {
                challenge.ConsumedAt = now;
                await _context.SaveChangesAsync(ct);

                return OtpVerifyResult.RegisterPartner();
            }

            if (string.IsNullOrWhiteSpace(name))
                return OtpVerifyResult.Fail("Enter your name to create your account.");

            challenge.ConsumedAt = now;

            var result = await _auth.RegisterCustomerAsync(new RegisterCustomerRequest
            {
                Name = name.Trim(),
                Phone = phone,
                Password = OtpCredentials.GenerateRandomPassword(),
            }, ct);

            if (!result.Succeeded || result.Response is null)
                return OtpVerifyResult.Fail(result.Error ?? "Could not create your account.");

            await _context.Users
                .Where(u => u.Id == result.Response.User.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.IsPhoneVerified, true)
                    .SetProperty(u => u.PhoneVerifiedAt, now), ct);

            return OtpVerifyResult.Ok(result);
        }
    }
}
