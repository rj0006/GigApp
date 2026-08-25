using System.ComponentModel.DataAnnotations;
using GigApp.Api.Dtos;

namespace GigApp.Api.ViewModels
{
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

    public class RegisterPartnerViewModel : RegisterCustomerViewModel
    {
        [Required(ErrorMessage = "Choose a skill category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose a skill category.")]
        [Display(Name = "Skill category")]
        public int SkillCategoryId { get; set; }

        // KYC is collected during sign-up — the form must be multipart.

        [Required(ErrorMessage = "Upload a selfie.")]
        [Display(Name = "Selfie")]
        public IFormFile? Selfie { get; set; }

        [Required(ErrorMessage = "Upload the front of your Aadhaar card.")]
        [Display(Name = "Aadhaar card — front")]
        public IFormFile? AadhaarFront { get; set; }

        [Required(ErrorMessage = "Upload the back of your Aadhaar card.")]
        [Display(Name = "Aadhaar card — back")]
        public IFormFile? AadhaarBack { get; set; }

        [Required(ErrorMessage = "Enter your Aadhaar number.")]
        [RegularExpression(ValidationPatterns.Aadhaar, ErrorMessage = ValidationPatterns.AadhaarMessage)]
        [Display(Name = "Aadhaar number")]
        public string AadhaarNumber { get; set; } = string.Empty;

        /// <summary>Populated by the controller for the picker.</summary>
        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();
    }

    public class CustomerDashboardViewModel
    {
        public string Name { get; set; } = string.Empty;
        public IReadOnlyList<GigTaskDto> Tasks { get; set; } = Array.Empty<GigTaskDto>();
        public CreateGigTaskRequest NewTask { get; set; } = new();
        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();

        /// <summary>
        /// Distinct partners across this customer's tasks — one modal each,
        /// rather than one per task row.
        /// </summary>
        public IReadOnlyList<PartnerPublicDto> Partners { get; set; } =
            Array.Empty<PartnerPublicDto>();

        /// <summary>Open bids per task id, for the bids modal.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<BidDto>> BidsByTask { get; set; } =
            new Dictionary<int, IReadOnlyList<BidDto>>();

        /// <summary>Saved addresses for the booking picker.</summary>
        public IReadOnlyList<AddressDto> Addresses { get; set; } = Array.Empty<AddressDto>();

        public IReadOnlyList<BidDto> BidsFor(int taskId) =>
            BidsByTask.TryGetValue(taskId, out var bids) ? bids : Array.Empty<BidDto>();
    }

    public class ProviderDashboardViewModel
    {
        public string Name { get; set; } = string.Empty;
        public PartnerDto? Profile { get; set; }

        /// <summary>Open tasks in this partner's category that they have not bid on yet.</summary>
        public IReadOnlyList<GigTaskDto> AvailableTasks { get; set; } = Array.Empty<GigTaskDto>();

        /// <summary>Bids placed. Countered ones need an answer from the partner.</summary>
        public IReadOnlyList<BidDto> MyBids { get; set; } = Array.Empty<BidDto>();

        /// <summary>Only tasks actually assigned — a bid alone does not appear here.</summary>
        public IReadOnlyList<GigTaskDto> MyJobs { get; set; } = Array.Empty<GigTaskDto>();

        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();

        public int CountersAwaitingReply => MyBids.Count(b => b.AwaitingPartner);
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

    // Every admin list page is server-side paged — see PagingExtensions.
    public class AdminPartnersViewModel
    {
        public PagedResult<PartnerDto> Partners { get; set; } = new();
        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();
        public bool? VerifiedFilter { get; set; }
        public int? CategoryFilter { get; set; }

        /// <summary>
        /// The account behind each partner, keyed by partner id. Filled only on
        /// the user-management list, where a super admin may reset a password or
        /// deactivate the account; empty everywhere else.
        /// </summary>
        public IReadOnlyDictionary<int, UserDto> Accounts { get; set; } =
            new Dictionary<int, UserDto>();
    }

    public class AdminUsersViewModel
    {
        public string Role { get; set; } = string.Empty;
        public string Heading { get; set; } = string.Empty;
        public PagedResult<UserDto> Users { get; set; } = new();
        public string? Search { get; set; }
    }

    public class AdminTasksViewModel
    {
        public PagedResult<GigTaskDto> Tasks { get; set; } = new();
        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();
        public string? StatusFilter { get; set; }
        public int? CategoryFilter { get; set; }
    }

    public class AdminCategoriesViewModel
    {
        public PagedResult<SkillCategoryDto> Categories { get; set; } = new();
        public bool ShowInactive { get; set; }
        public string? Search { get; set; }
    }

    public class AdminServiceItemsViewModel
    {
        public PagedResult<ServiceItemDto> Items { get; set; } = new();
        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();
        public int? CategoryFilter { get; set; }
        public bool ShowInactive { get; set; }
    }

    public class ServiceItemFormViewModel
    {
        public int? Id { get; set; }
        public SaveServiceItemRequest Form { get; set; } = new();

        public IReadOnlyList<SkillCategoryOptionDto> Categories { get; set; } =
            Array.Empty<SkillCategoryOptionDto>();

        public bool IsEdit => Id is not null;
        public string Heading => IsEdit ? "Edit service" : "New service";

        /// <summary>Set on edit so the form can warn before deactivating something in use.</summary>
        public int TaskCount { get; set; }
    }

    public class AdminPriceInsightsViewModel
    {
        public IReadOnlyList<PriceInsightDto> Insights { get; set; } = Array.Empty<PriceInsightDto>();

        public int WithEnoughData => Insights.Count(i => i.HasEnoughData);
        public int NeedingSplit => Insights.Count(i => i.HasEnoughData && i.IsTooVaried);
        public int TotalCompleted => Insights.Sum(i => i.CompletedCount);
    }

    public class SkillCategoryFormViewModel
    {
        public int? Id { get; set; }
        public SaveSkillCategoryRequest Form { get; set; } = new();

        public bool IsEdit => Id is not null;
        public string Heading => IsEdit ? "Edit category" : "New category";

        /// <summary>Set on edit so the form can warn before deactivating something in use.</summary>
        public int PartnerCount { get; set; }
        public int TaskCount { get; set; }
    }
}
