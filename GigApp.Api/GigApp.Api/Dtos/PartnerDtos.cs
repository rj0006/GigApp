using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class PartnerDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsPhoneVerified { get; set; }

        public int SkillCategoryId { get; set; }
        public string SkillCategoryName { get; set; } = string.Empty;
        public string KycStatus { get; set; } = Models.KycStatus.NotSubmitted;
        public string? KycRejectionReason { get; set; }
        public DateTime? KycReviewedAt { get; set; }

        public string KycLabel => Models.KycStatus.Label(KycStatus);
        public string KycBadgeClass => Models.KycStatus.BadgeClass(KycStatus);

        public bool IsVerified => KycStatus == Models.KycStatus.Approved;
        public bool IsRejected => KycStatus == Models.KycStatus.Rejected;
        public bool IsAwaitingReview => KycStatus == Models.KycStatus.Pending;

        public bool IsAvailable { get; set; }

        // KYC. Only the partner themselves and admins ever receive a PartnerDto.
        public string? SelfieFileName { get; set; }
        public string? AadhaarFrontFileName { get; set; }
        public string? AadhaarBackFileName { get; set; }
        public string? AadhaarNumber { get; set; }

        /// <summary>Authorized URLs — the files are not reachable directly.</summary>
        public string? SelfieUrl => KycUrl(SelfieFileName);
        public string? AadhaarFrontUrl => KycUrl(AadhaarFrontFileName);
        public string? AadhaarBackUrl => KycUrl(AadhaarBackFileName);

        public bool HasCompleteKyc =>
            SelfieUrl is not null && AadhaarFrontUrl is not null
            && AadhaarBackUrl is not null && !string.IsNullOrWhiteSpace(AadhaarNumber);

        /// <summary>Shown in lists; the full number is only on the review screen.</summary>
        public string? MaskedAadhaar => string.IsNullOrWhiteSpace(AadhaarNumber)
            ? null
            : $"XXXX XXXX {AadhaarNumber[^4..]}";

        private static string? KycUrl(string? fileName) =>
            string.IsNullOrWhiteSpace(fileName) ? null : $"/api/files/kyc/{fileName}";

        public DateTime CreatedAt { get; set; }

        /// <summary>Requires the User navigation to be Included.</summary>
        public static PartnerDto From(Partner partner) => new()
        {
            Id = partner.Id,
            UserId = partner.UserId,
            Name = partner.User?.Name ?? string.Empty,
            Phone = partner.User?.Phone ?? string.Empty,
            Email = partner.User?.Email,
            IsPhoneVerified = partner.User?.IsPhoneVerified ?? false,
            SkillCategoryId = partner.SkillCategoryId,
            SkillCategoryName = partner.SkillCategory?.Name ?? string.Empty,
            KycStatus = partner.KycStatus,
            KycRejectionReason = partner.KycRejectionReason,
            KycReviewedAt = partner.KycReviewedAt,
            IsAvailable = partner.IsAvailable,
            SelfieFileName = partner.SelfieFileName,
            AadhaarFrontFileName = partner.AadhaarFrontFileName,
            AadhaarBackFileName = partner.AadhaarBackFileName,
            AadhaarNumber = partner.AadhaarNumber,
            CreatedAt = partner.CreatedAt,
        };
    }

    public class UpdateAvailabilityRequest
    {
        [Required]
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Re-submitting KYC. Every field is optional here — a partner may replace
    /// just one image — but an admin still cannot approve until all are present.
    /// </summary>
    public class UpdateKycRequest
    {
        [Display(Name = "Selfie")]
        public IFormFile? Selfie { get; set; }

        [Display(Name = "Aadhaar front")]
        public IFormFile? AadhaarFront { get; set; }

        [Display(Name = "Aadhaar back")]
        public IFormFile? AadhaarBack { get; set; }

        [RegularExpression(ValidationPatterns.Aadhaar, ErrorMessage = ValidationPatterns.AadhaarMessage)]
        [Display(Name = "Aadhaar number")]
        public string? AadhaarNumber { get; set; }

        public bool HasAnything =>
            Selfie is not null || AadhaarFront is not null
            || AadhaarBack is not null || !string.IsNullOrWhiteSpace(AadhaarNumber);
    }

    public class VerifyPartnerRequest
    {
        [Required]
        public bool IsVerified { get; set; }

        /// <summary>
        /// Required when rejecting. A rejection with no reason leaves the
        /// partner unable to work and with no idea what to fix.
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }
    }

    public class UpdatePartnerProfileRequest
    {
        [Required(ErrorMessage = "Choose a skill category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose a skill category.")]
        [Display(Name = "Skill category")]
        public int SkillCategoryId { get; set; }
    }
}
