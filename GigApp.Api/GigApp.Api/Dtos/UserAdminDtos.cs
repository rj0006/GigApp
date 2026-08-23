using System.ComponentModel.DataAnnotations;

namespace GigApp.Api.Dtos
{
    public class AdminResetPasswordRequest
    {
        /// <summary>
        /// Generate a strong password instead of supplying one. Preferred — a
        /// typed password is usually weak and reused across accounts.
        /// </summary>
        [Display(Name = "Generate a strong password")]
        public bool Generate { get; set; } = true;

        /// <summary>Only used when <see cref="Generate"/> is false.</summary>
        [Display(Name = "New password")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Give a reason for this reset.")]
        [StringLength(300)]
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public class SetUserActiveRequest
    {
        public bool IsActive { get; set; }

        /// <summary>Required when deactivating, so the audit trail says why.</summary>
        [StringLength(300)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }
    }

    public class UserAdminResult
    {
        public bool Succeeded { get; init; }
        public string? Error { get; init; }
        public UserDto? User { get; init; }

        /// <summary>
        /// The new password, returned once so it can be passed to the user. It
        /// is never stored and the audit trail masks it.
        /// </summary>
        public string? GeneratedPassword { get; init; }

        public static UserAdminResult Ok(UserDto user, string? password = null) =>
            new() { Succeeded = true, User = user, GeneratedPassword = password };

        public static UserAdminResult Fail(string error) =>
            new() { Succeeded = false, Error = error };
    }
}
