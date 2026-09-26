using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class RequestOtpRequest
    {
        [Required]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        public string Phone { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class VerifyOtpRequest
    {
        [Required]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 2)]
        public string? Name { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class AuthSettingsDto
    {
        public string CustomerLoginMode { get; set; } = LoginMode.Password;
        public string PartnerLoginMode { get; set; } = LoginMode.Password;
    }

    public class OtpRequestApiRequest
    {
        [Required]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Role { get; set; } = string.Empty;
    }

    public class OtpVerifyApiRequest
    {
        [Required]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Role { get; set; } = string.Empty;

        [Required, StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 2)]
        public string? Name { get; set; }
    }

    public class OtpVerifyApiResponse
    {
        public bool RequiresPartnerRegistration { get; set; }
        public AuthResponse? Auth { get; set; }
    }
}
