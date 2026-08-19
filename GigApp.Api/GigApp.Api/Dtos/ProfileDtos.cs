using System.ComponentModel.DataAnnotations;

namespace GigApp.Api.Dtos
{
    public class UpdateProfileRequest
    {
        [Required, StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        [Display(Name = "Mobile number")]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress, StringLength(256)]
        [Display(Name = "Email (optional)")]
        public string? Email { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a new password.")]
        [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class UpdateProfilePhotoRequest
    {
        [Required(ErrorMessage = "Choose an image to upload.")]
        [Display(Name = "Profile photo")]
        public IFormFile? Photo { get; set; }
    }

    /// <summary>One field that changed between two audit rows.</summary>
    public class FieldChangeDto
    {
        public string Field { get; set; } = string.Empty;
        public string? From { get; set; }
        public string? To { get; set; }
    }

    /// <summary>
    /// A single profile change, reconstructed from the audit trail. The "old"
    /// values come from the previous row for the same user — there is no
    /// OldValues column by design.
    /// </summary>
    public class ProfileChangeDto
    {
        public DateTime ChangedAt { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Remark { get; set; }
        public IReadOnlyList<FieldChangeDto> Changes { get; set; } = Array.Empty<FieldChangeDto>();
    }

    public class ProfileResult
    {
        public bool Succeeded { get; init; }
        public string? Error { get; init; }
        public UserDto? User { get; init; }

        public static ProfileResult Ok(UserDto user) => new() { Succeeded = true, User = user };
        public static ProfileResult Fail(string error) => new() { Succeeded = false, Error = error };
    }
}
