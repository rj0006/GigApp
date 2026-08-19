using System.ComponentModel.DataAnnotations;

namespace GigApp.Api.Dtos
{
    public class LoginRequest
    {
        /// <summary>
        /// Mobile number or email address. Accepting either keeps the contract
        /// stable when phone becomes the primary identifier.
        /// </summary>
        [Required, StringLength(256)]
        public string Identifier { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterCustomerRequest
    {
        [Required, StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Required — this is the account's primary identifier.</summary>
        [Required]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        public string Phone { get; set; } = string.Empty;

        /// <summary>Optional. Phone-first signups may omit it entirely.</summary>
        [EmailAddress, StringLength(256)]
        public string? Email { get; set; }

        [Required]
        [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordMessage)]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterPartnerRequest : RegisterCustomerRequest
    {
        /// <summary>Must reference an active row in the skill category master.</summary>
        [Required(ErrorMessage = "Choose a skill category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose a skill category.")]
        [Display(Name = "Skill category")]
        public int SkillCategoryId { get; set; }

        // KYC is collected up front — a partner cannot exist without it.
        // This makes the endpoint multipart/form-data, not JSON.

        [Required(ErrorMessage = "Upload a selfie.")]
        [Display(Name = "Selfie")]
        public IFormFile? Selfie { get; set; }

        [Required(ErrorMessage = "Upload the front of your Aadhaar card.")]
        [Display(Name = "Aadhaar front")]
        public IFormFile? AadhaarFront { get; set; }

        [Required(ErrorMessage = "Upload the back of your Aadhaar card.")]
        [Display(Name = "Aadhaar back")]
        public IFormFile? AadhaarBack { get; set; }

        [Required(ErrorMessage = "Enter your Aadhaar number.")]
        [RegularExpression(ValidationPatterns.Aadhaar, ErrorMessage = ValidationPatterns.AadhaarMessage)]
        [Display(Name = "Aadhaar number")]
        public string AadhaarNumber { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public UserDto User { get; set; } = new();
    }
}
