using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct = default);
        Task<AuthResult> RegisterPartnerAsync(RegisterPartnerRequest request, CancellationToken ct = default);
        Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<AuthResult> LoginWithOtpAsync(string phone, string role, CancellationToken ct = default);
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IRefreshTokenService _refreshTokens;
        private readonly IHttpContextAccessor _http;
        private readonly IFileStorageService _storage;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context,
            ITokenService tokenService,
            IRefreshTokenService refreshTokens,
            IHttpContextAccessor http,
            IFileStorageService storage,
            ILogger<AuthService> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _refreshTokens = refreshTokens;
            _http = http;
            _storage = storage;
            _logger = logger;
        }

        public Task<AuthResult> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct = default) =>
            RegisterAsync(request, UserRoles.Customer, partnerProfile: null, ct);

        public async Task<AuthResult> RegisterPartnerAsync(RegisterPartnerRequest request, CancellationToken ct = default)
        {
            // A partner's skill must come from the master, and an inactive
            // category should not be selectable for a brand-new account.
            var categoryExists = await _context.SkillCategories
                .AnyAsync(c => c.Id == request.SkillCategoryId && c.IsActive, ct);

            if (!categoryExists)
                return AuthResult.Fail("Choose a valid skill category.");

            // Store the three KYC images first. Track what landed on disk so a
            // later failure does not leave orphaned files behind.
            var stored = new List<string>();

            var selfie = await SaveKycAsync(request.Selfie, stored, ct);
            if (selfie.Error is not null) return await FailAndCleanUpAsync(selfie.Error, stored);

            var aadhaarFront = await SaveKycAsync(request.AadhaarFront, stored, ct);
            if (aadhaarFront.Error is not null) return await FailAndCleanUpAsync(aadhaarFront.Error, stored);

            var aadhaarBack = await SaveKycAsync(request.AadhaarBack, stored, ct);
            if (aadhaarBack.Error is not null) return await FailAndCleanUpAsync(aadhaarBack.Error, stored);

            var result = await RegisterAsync(request, UserRoles.Partner, new Partner
            {
                SkillCategoryId = request.SkillCategoryId,
                SelfieFileName = selfie.FileName,
                AadhaarFrontFileName = aadhaarFront.FileName,
                AadhaarBackFileName = aadhaarBack.FileName,
                AadhaarNumber = request.AadhaarNumber.Trim(),
                KycStatus = Models.KycStatus.Pending,   // documents arrive with sign-up, so review starts immediately
                IsAvailable = true,
            }, ct);

            // Duplicate phone, duplicate email, database error — drop the uploads.
            if (!result.Succeeded) CleanUp(stored);

            return result;
        }

        private async Task<(string? FileName, string? Error)> SaveKycAsync(
            IFormFile? file, List<string> stored, CancellationToken ct)
        {
            var saved = await _storage.SaveAsync(file, FileCategory.KycDocument, ct);

            if (!saved.Succeeded) return (null, saved.Error);

            stored.Add(saved.FileName!);
            return (saved.FileName, null);
        }

        private Task<AuthResult> FailAndCleanUpAsync(string error, List<string> stored)
        {
            CleanUp(stored);
            return Task.FromResult(AuthResult.Fail(error));
        }

        private void CleanUp(IEnumerable<string> fileNames)
        {
            foreach (var name in fileNames)
                _storage.Delete(name, FileCategory.KycDocument);
        }

        private async Task<AuthResult> RegisterAsync(
            RegisterCustomerRequest request,
            string role,
            Partner? partnerProfile,
            CancellationToken ct)
        {
            var email = NormalizeEmail(request.Email);
            var phone = request.Phone.Trim();

            if (await _context.Users.AnyAsync(u => u.Phone == phone && u.Role == role, ct))
                return AuthResult.Fail($"A {role} account with this phone number already exists.");

            if (email is not null && await _context.Users.AnyAsync(u => u.Email == email && u.Role == role, ct))
                return AuthResult.Fail($"A {role} account with this email already exists.");

            var user = new User
            {
                Name = request.Name.Trim(),
                Phone = phone,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                CreatedAt = DateTime.UtcNow,
            };

            if (partnerProfile is not null)
            {
                partnerProfile.CreatedAt = DateTime.UtcNow;
                user.PartnerProfile = partnerProfile;
            }

            _context.Users.Add(user);

            try
            {
                // The uniqueness checks above race; the unique indexes are what
                // actually guarantee it, so a concurrent signup surfaces here.
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return AuthResult.Fail($"A {role} account with this email or phone number already exists.");
            }

            _logger.LogInformation("Registered {Role} account {UserId}", role, user.Id);
            return AuthResult.Ok(await BuildResponseAsync(user, ct));
        }

        public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            var identifier = request.Identifier.Trim();
            var query = _context.Users.Include(u => u.PartnerProfile).AsQueryable();

            query = identifier.Contains('@')
                ? query.Where(u => u.Email == identifier.ToLowerInvariant())
                : query.Where(u => u.Phone == identifier);

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var role = request.Role.Trim();

                query = UserRoles.IsAdminRole(role)
                    ? query.Where(u => u.Role == UserRoles.Admin || u.Role == UserRoles.SuperAdmin)
                    : query.Where(u => u.Role == role);
            }

            var candidates = await query.ToListAsync(ct);

            var matched = candidates
                .Where(u => VerifyPassword(request.Password, u.PasswordHash))
                .ToList();

            if (matched.Count == 0)
            {
                _logger.LogWarning("Failed login attempt for {Identifier}", identifier);
                return AuthResult.Fail("Invalid credentials.");
            }

            if (matched.Count > 1)
            {
                var roles = string.Join(" and ", matched.Select(u => u.Role).Order());
                return AuthResult.Fail(
                    $"This login is used by more than one account ({roles}). " +
                    "Sign in from the portal for the account you want.");
            }

            var user = matched[0];

            if (!user.IsActive)
            {
                _logger.LogWarning("Login blocked for deactivated user {UserId}", user.Id);
                return AuthResult.Fail("This account has been deactivated. Contact support.");
            }

            return AuthResult.Ok(await BuildResponseAsync(user, ct));
        }

        public async Task<AuthResult> LoginWithOtpAsync(
            string phone, string role, CancellationToken ct = default)
        {
            var user = await _context.Users.Include(u => u.PartnerProfile)
                .FirstOrDefaultAsync(u => u.Phone == phone && u.Role == role, ct);

            if (user is null) return AuthResult.Fail("No account found for this number.");

            if (!user.IsActive)
            {
                _logger.LogWarning("OTP login blocked for deactivated user {UserId}", user.Id);
                return AuthResult.Fail("This account has been deactivated. Contact support.");
            }

            if (!user.IsPhoneVerified)
            {
                user.IsPhoneVerified = true;
                user.PhoneVerifiedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }

            _logger.LogInformation("OTP login for {Role} account {UserId}", role, user.Id);
            return AuthResult.Ok(await BuildResponseAsync(user, ct));
        }

        private async Task<AuthResponse> BuildResponseAsync(User user, CancellationToken ct)
        {
            var (token, expiresAtUtc) = _tokenService.CreateToken(user);

            var request = _http.HttpContext?.Request;
            var deviceLabel = DeviceLabel.FromUserAgent(request?.Headers.UserAgent.ToString());
            var ipAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

            var (refreshToken, refreshExpiresAtUtc) =
                await _refreshTokens.IssueAsync(user.Id, deviceLabel, ipAddress, ct);

            return new AuthResponse
            {
                Token = token,
                ExpiresAtUtc = expiresAtUtc,
                RefreshToken = refreshToken,
                RefreshExpiresAtUtc = refreshExpiresAtUtc,
                User = UserDto.From(user),
            };
        }

        private static bool VerifyPassword(string password, string hash)
        {
            // Accounts seeded before auth existed carry an empty hash; BCrypt
            // throws on a malformed salt, so reject them explicitly instead.
            if (string.IsNullOrEmpty(hash)) return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        /// <summary>Lowercases and trims; blank or absent becomes null so the
        /// unique index treats it as "no email" rather than an empty string.</summary>
        private static string? NormalizeEmail(string? email) =>
            string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }

    public class AuthResult
    {
        public bool Succeeded { get; private init; }
        public string? Error { get; private init; }
        public AuthResponse? Response { get; private init; }

        public static AuthResult Ok(AuthResponse response) =>
            new() { Succeeded = true, Response = response };

        public static AuthResult Fail(string error) =>
            new() { Succeeded = false, Error = error };
    }
}
