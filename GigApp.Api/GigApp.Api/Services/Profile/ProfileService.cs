using System.Text.Json;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services.Files;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Profile
{
    /// <summary>
    /// All profile logic lives here so the API and the three Razor portals share
    /// one implementation — the mobile apps get the same rules for free.
    /// </summary>
    public interface IProfileService
    {
        Task<UserDto?> GetAsync(int userId, CancellationToken ct = default);
        Task<ProfileResult> UpdateAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default);
        Task<ProfileResult> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);
        Task<ProfileResult> UpdatePhotoAsync(int userId, IFormFile? photo, CancellationToken ct = default);
        Task<IReadOnlyList<ProfileChangeDto>> GetHistoryAsync(int userId, int take = 20, CancellationToken ct = default);
    }

    public class ProfileService : IProfileService
    {
        /// <summary>Fields worth showing in the history, and how to label them.</summary>
        private static readonly (string Key, string Label)[] TrackedFields =
        {
            ("name", "Name"),
            ("phone", "Mobile number"),
            ("email", "Email"),
            ("profileImageFileName", "Profile photo"),
        };

        private readonly AppDbContext _context;
        private readonly IFileStorageService _storage;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            AppDbContext context, IFileStorageService storage, ILogger<ProfileService> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        public async Task<UserDto?> GetAsync(int userId, CancellationToken ct = default)
        {
            var user = await LoadAsync(userId, track: false, ct);
            return user is null ? null : UserDto.From(user);
        }

        public async Task<ProfileResult> UpdateAsync(
            int userId, UpdateProfileRequest request, CancellationToken ct = default)
        {
            var user = await LoadAsync(userId, track: true, ct);
            if (user is null) return ProfileResult.Fail("Account not found.");

            var phone = request.Phone.Trim();
            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Id != userId && u.Phone == phone, ct))
                return ProfileResult.Fail("Another account already uses this phone number.");

            if (email is not null && await _context.Users.AnyAsync(u => u.Id != userId && u.Email == email, ct))
                return ProfileResult.Fail("Another account already uses this email.");

            // Changing the number invalidates the verification that was done
            // against the old one. Once OTP lands, this is what forces a re-verify.
            if (!string.Equals(user.Phone, phone, StringComparison.Ordinal))
            {
                user.IsPhoneVerified = false;
                user.PhoneVerifiedAt = null;
            }

            user.Name = request.Name.Trim();
            user.Phone = phone;
            user.Email = email;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return ProfileResult.Fail("Another account already uses this phone number or email.");
            }

            return ProfileResult.Ok(UserDto.From(user));
        }

        public async Task<ProfileResult> ChangePasswordAsync(
            int userId, ChangePasswordRequest request, CancellationToken ct = default)
        {
            var user = await LoadAsync(userId, track: true, ct);
            if (user is null) return ProfileResult.Fail("Account not found.");

            // Proving the current password stops someone with a borrowed session
            // from locking the real owner out.
            if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
                return ProfileResult.Fail("Your current password is not correct.");

            if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
                return ProfileResult.Fail("The new password must be different from the current one.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Password changed for user {UserId}", userId);

            return ProfileResult.Ok(UserDto.From(user));
        }

        public async Task<ProfileResult> UpdatePhotoAsync(
            int userId, IFormFile? photo, CancellationToken ct = default)
        {
            var user = await LoadAsync(userId, track: true, ct);
            if (user is null) return ProfileResult.Fail("Account not found.");

            var saved = await _storage.SaveAsync(photo, FileCategory.ProfileImage, ct);
            if (!saved.Succeeded) return ProfileResult.Fail(saved.Error!);

            var previous = user.ProfileImageFileName;
            user.ProfileImageFileName = saved.FileName;

            await _context.SaveChangesAsync(ct);

            // Only once the row is safely saved, or a failure would leave the
            // user pointing at a file that no longer exists.
            _storage.Delete(previous, FileCategory.ProfileImage);

            return ProfileResult.Ok(UserDto.From(user));
        }

        /// <summary>
        /// Rebuilds "what changed, and when" from the audit trail by comparing
        /// each row's stored record against the previous row for the same user.
        /// </summary>
        public async Task<IReadOnlyList<ProfileChangeDto>> GetHistoryAsync(
            int userId, int take = 20, CancellationToken ct = default)
        {
            var docNo = userId.ToString();

            var rows = await _context.TrackingLogs
                .AsNoTracking()
                .Where(t => t.FormType == "Profile" && t.DocNo == docNo)
                .OrderBy(t => t.TransactionDate)
                .Select(t => new { t.TransactionDate, t.Payload, t.Remark, t.RequestPath })
                .ToListAsync(ct);

            var history = new List<ProfileChangeDto>();
            Dictionary<string, string?>? previous = null;

            foreach (var row in rows)
            {
                var current = ReadResultFields(row.Payload);

                var changes = previous is null
                    ? new List<FieldChangeDto>()
                    : TrackedFields
                        .Where(f => previous.GetValueOrDefault(f.Key) != current.GetValueOrDefault(f.Key))
                        .Select(f => new FieldChangeDto
                        {
                            Field = f.Label,
                            From = Display(f.Key, previous.GetValueOrDefault(f.Key)),
                            To = Display(f.Key, current.GetValueOrDefault(f.Key)),
                        })
                        .ToList();

                history.Add(new ProfileChangeDto
                {
                    ChangedAt = row.TransactionDate,
                    Action = DescribeAction(row.RequestPath, changes),
                    Remark = row.Remark,
                    Changes = changes,
                });

                previous = current;
            }

            history.Reverse();   // newest first
            return history.Take(take).ToList();
        }

        private Task<User?> LoadAsync(int userId, bool track, CancellationToken ct)
        {
            var query = _context.Users
                .Include(u => u.PartnerProfile)!.ThenInclude(p => p!.SkillCategory)
                .AsQueryable();

            if (!track) query = query.AsNoTracking();

            return query.FirstOrDefaultAsync(u => u.Id == userId, ct);
        }

        /// <summary>Pulls the tracked fields out of the audit row's stored record.</summary>
        private static Dictionary<string, string?> ReadResultFields(string payload)
        {
            var fields = new Dictionary<string, string?>();

            try
            {
                using var document = JsonDocument.Parse(payload);

                if (!document.RootElement.TryGetProperty("result", out var result)
                    || result.ValueKind != JsonValueKind.Object)
                    return fields;

                foreach (var (key, _) in TrackedFields)
                {
                    fields[key] = result.TryGetProperty(key, out var value)
                        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                        ? value.ToString()
                        : null;
                }
            }
            catch (JsonException)
            {
                // A malformed row should not break the whole history.
            }

            return fields;
        }

        private static string DescribeAction(string? requestPath, IReadOnlyList<FieldChangeDto> changes)
        {
            if (requestPath?.Contains("password", StringComparison.OrdinalIgnoreCase) == true)
                return "Password changed";

            if (requestPath?.Contains("photo", StringComparison.OrdinalIgnoreCase) == true)
                return "Profile photo updated";

            return changes.Count == 0 ? "Profile saved" : "Profile updated";
        }

        /// <summary>File names mean nothing to a reader; say "a photo" instead.</summary>
        private static string? Display(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "—";
            return key == "profileImageFileName" ? "a photo" : value;
        }

        private static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;

            try { return BCrypt.Net.BCrypt.Verify(password, hash); }
            catch (BCrypt.Net.SaltParseException) { return false; }
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }
}
