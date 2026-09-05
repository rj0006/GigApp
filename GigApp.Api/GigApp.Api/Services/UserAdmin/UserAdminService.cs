using System.Security.Cryptography;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.UserAdmin
{
    /// <summary>
    /// Operations a super admin can perform on someone else's account.
    ///
    /// There is deliberately no "read a password" method. Passwords are stored
    /// as BCrypt hashes, which are one-way by design — the plaintext does not
    /// exist anywhere to be read, and making it readable would mean a database
    /// leak handed every user's password to an attacker. Support resets a
    /// password; it never reads one.
    /// </summary>
    public interface IUserAdminService
    {
        Task<UserAdminResult> ResetPasswordAsync(
            int actingUserId, int targetUserId, AdminResetPasswordRequest request, CancellationToken ct = default);

        Task<UserAdminResult> SetActiveAsync(
            int actingUserId, int targetUserId, SetUserActiveRequest request, CancellationToken ct = default);
    }

    public class UserAdminService : IUserAdminService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserAdminService> _logger;

        public UserAdminService(AppDbContext context, ILogger<UserAdminService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserAdminResult> ResetPasswordAsync(
            int actingUserId, int targetUserId, AdminResetPasswordRequest request, CancellationToken ct = default)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct);
            if (user is null) return UserAdminResult.Fail("Account not found.");

            // A typed password wins. Leaving the field empty is what asks for a
            // generated one, so the box the administrator filled in is never
            // silently thrown away.
            var typed = request.NewPassword?.Trim();

            if (!string.IsNullOrEmpty(typed)
                && !System.Text.RegularExpressions.Regex.IsMatch(typed, ValidationPatterns.Password))
            {
                return UserAdminResult.Fail(ValidationPatterns.PasswordMessage);
            }

            var newPassword = string.IsNullOrEmpty(typed) ? GeneratePassword() : typed;
            var wasGenerated = string.IsNullOrEmpty(typed);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Super admin {ActingUserId} reset the password for user {TargetUserId}",
                actingUserId, targetUserId);

            // The plaintext is returned once, to this caller, so it can be given
            // to the user. It is never stored, and the audit trail redacts it.
            return UserAdminResult.Ok(UserDto.From(user), newPassword, wasGenerated);
        }

        public async Task<UserAdminResult> SetActiveAsync(
            int actingUserId, int targetUserId, SetUserActiveRequest request, CancellationToken ct = default)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct);
            if (user is null) return UserAdminResult.Fail("Account not found.");

            // Locking yourself out is never the intent, and recovering from it
            // needs database access.
            if (targetUserId == actingUserId)
                return UserAdminResult.Fail("You cannot deactivate your own account.");

            if (!request.IsActive)
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                    return UserAdminResult.Fail("Give a reason before deactivating an account.");

                // Leaving zero active super admins would lock everyone out of
                // the operations only a super admin can perform.
                if (user.Role == UserRoles.SuperAdmin)
                {
                    var remaining = await _context.Users.CountAsync(
                        u => u.Role == UserRoles.SuperAdmin && u.IsActive && u.Id != targetUserId, ct);

                    if (remaining == 0)
                        return UserAdminResult.Fail("This is the last active super admin.");
                }
            }

            user.IsActive = request.IsActive;
            user.DeactivatedAt = request.IsActive ? null : DateTime.UtcNow;
            user.DeactivationReason = request.IsActive ? null : request.Reason?.Trim();

            await _context.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Super admin {ActingUserId} set user {TargetUserId} active={IsActive}",
                actingUserId, targetUserId, request.IsActive);

            return UserAdminResult.Ok(UserDto.From(user));
        }

        /// <summary>
        /// A readable but strong password. Built from a cryptographic RNG rather
        /// than Random, and guaranteed to satisfy the same policy users face.
        /// </summary>
        private static string GeneratePassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // no I or O
            const string lower = "abcdefghijkmnpqrstuvwxyz";   // no l or o
            const string digits = "23456789";                  // no 0 or 1
            const string symbols = "@#$%&*";

            var all = upper + lower + digits + symbols;

            // One of each class first, so the result always passes the policy.
            var chars = new List<char>
            {
                Pick(upper), Pick(lower), Pick(digits), Pick(symbols),
            };

            while (chars.Count < 12) chars.Add(Pick(all));

            // Shuffle, or the class order would be predictable.
            for (var i = chars.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }

        private static char Pick(string source) =>
            source[RandomNumberGenerator.GetInt32(source.Length)];
    }
}
