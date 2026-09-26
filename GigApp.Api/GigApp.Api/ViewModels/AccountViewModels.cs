using System.ComponentModel.DataAnnotations;
using GigApp.Api.Dtos;
using GigApp.Api.Models;

namespace GigApp.Api.ViewModels
{
    public class OtpAuthViewModel
    {
        public string PortalSlug { get; set; } = string.Empty;
        public string PortalLabel { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }

        public string RequestPath => $"/{PortalSlug}/otp/request";
        public string VerifyPath => $"/{PortalSlug}/otp/verify";
        public string PasswordFallbackPath => $"/{PortalSlug}/login?mode={LoginMode.Password}";
    }

    public class AuthSettingsViewModel
    {
        [Required]
        public string CustomerLoginMode { get; set; } = LoginMode.Password;

        [Required]
        public string PartnerLoginMode { get; set; } = LoginMode.Password;
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Enter your mobile number or email.")]
        [Display(Name = "Mobile number or email")]
        public string Identifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class RegisterCustomerViewModel
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

        [Required]
        [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordMessage)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterPartnerShellViewModel
    {
        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();
    }


    public class CatalogCategoryViewModel
    {
        public SkillCategoryDto Category { get; set; } = new();
        public int ServiceCount { get; set; }
        public decimal? StartingFrom { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int CustomerCount { get; set; }
        public int PartnerCount { get; set; }
        public int PendingKycCount { get; set; }
        public int OpenTaskCount { get; set; }
        public int CategoryCount { get; set; }
        public IReadOnlyList<PartnerDto> PendingPartners { get; set; } = Array.Empty<PartnerDto>();
        public IReadOnlyList<GigTaskDto> RecentTasks { get; set; } = Array.Empty<GigTaskDto>();
    }

    public class PartnerLedgerShellViewModel
    {
        public int PartnerId { get; set; }
    }

    public class PlanFormShellViewModel
    {
        public int? Id { get; set; }
        public bool IsNew => Id is null;
    }

    public class TaxFormShellViewModel
    {
        public int? Id { get; set; }
        public bool IsNew => Id is null;
    }

    public class NotificationsViewModel
    {
        public PagedResult<NotificationDto> Notifications { get; set; } = new();
        public string PortalSlug { get; set; } = "customer";
    }

    public class UserMenuViewModel
    {
        public string? DisplayName { get; set; }
        public string PortalSlug { get; set; } = "customer";
        public string? PhotoUrl { get; set; }
        public int NotificationCount { get; set; }
        public string? NotificationHref { get; set; }
        public string NotificationLabel { get; set; } = "unread notifications";

        public IReadOnlyList<NotificationDto> Recent { get; set; } = Array.Empty<NotificationDto>();

        public string ProfilePath => $"/{PortalSlug}/profile";
        public string LogoutPath => $"/{PortalSlug}/logout";

        public string Initial => string.IsNullOrWhiteSpace(DisplayName)
            ? "?"
            : DisplayName.Trim()[..1].ToUpperInvariant();
    }

    public class MenuFormShellViewModel
    {
        public int? Id { get; set; }
        public bool IsNew => Id is null;
    }

    public class UsersShellViewModel
    {
        public string Role { get; set; } = string.Empty;
        public string Heading { get; set; } = string.Empty;
        public bool IncludeSuperAdmins { get; set; }
    }

    public class BannerFormShellViewModel
    {
        public int? Id { get; set; }
        public bool IsEdit => Id is not null;
        public string Heading => IsEdit ? "Edit banner" : "New banner";
    }

    public class ServiceFormShellViewModel
    {
        public int? Id { get; set; }
        public bool IsEdit => Id is not null;
        public string Heading => IsEdit ? "Edit service" : "New service";
    }

    public class AdminPriceInsightsViewModel
    {
        public IReadOnlyList<PriceInsightDto> Insights { get; set; } = Array.Empty<PriceInsightDto>();

        public int WithEnoughData => Insights.Count(i => i.HasEnoughData);
        public int NeedingSplit => Insights.Count(i => i.HasEnoughData && i.IsTooVaried);
        public int TotalCompleted => Insights.Sum(i => i.CompletedCount);
    }

    public class CategoryFormShellViewModel
    {
        public int? Id { get; set; }
        public bool IsEdit => Id is not null;
        public string Heading => IsEdit ? "Edit category" : "New category";
    }
}
