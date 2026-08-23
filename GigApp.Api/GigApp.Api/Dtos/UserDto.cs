using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    /// <summary>
    /// Safe outward-facing view of a user. Deliberately has no PasswordHash —
    /// map through here rather than returning the entity.
    /// </summary>
    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsPhoneVerified { get; set; }
        public bool IsActive { get; set; }
        public string? DeactivationReason { get; set; }
        public string RoleLabel => UserRoles.Label(Role);

        public string? ProfileImageFileName { get; set; }

        /// <summary>Served straight from wwwroot; null when no photo was uploaded.</summary>
        public string? ProfileImageUrl =>
            string.IsNullOrWhiteSpace(ProfileImageFileName) ? null : $"/uploads/profile/{ProfileImageFileName}";

        public DateTime CreatedAt { get; set; }

        // Populated only for partners.
        public PartnerProfileDto? PartnerProfile { get; set; }

        public static UserDto From(User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Phone = user.Phone,
            Email = user.Email,
            Role = user.Role,
            IsPhoneVerified = user.IsPhoneVerified,
            IsActive = user.IsActive,
            DeactivationReason = user.DeactivationReason,
            ProfileImageFileName = user.ProfileImageFileName,
            CreatedAt = user.CreatedAt,
            PartnerProfile = user.PartnerProfile is null
                ? null
                : PartnerProfileDto.From(user.PartnerProfile),
        };
    }

    public class PartnerProfileDto
    {
        public int Id { get; set; }
        public int SkillCategoryId { get; set; }

        /// <summary>Empty unless the SkillCategory navigation was Included.</summary>
        public string SkillCategoryName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Whether every KYC document is on file. The images themselves are not
        /// exposed here — this DTO is nested inside UserDto, which is returned
        /// from the account endpoints; use PartnerDto for the review screen.
        /// </summary>
        public bool HasCompleteKyc { get; set; }

        public static PartnerProfileDto From(Partner partner) => new()
        {
            Id = partner.Id,
            SkillCategoryId = partner.SkillCategoryId,
            SkillCategoryName = partner.SkillCategory?.Name ?? string.Empty,
            IsVerified = partner.IsVerified,
            IsAvailable = partner.IsAvailable,
            HasCompleteKyc = partner.HasCompleteKyc,
        };
    }
}
