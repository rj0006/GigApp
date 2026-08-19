using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    /// <summary>
    /// What a customer is allowed to see about the partner working on their task.
    ///
    /// Deliberately separate from <see cref="PartnerDto"/>, which carries the
    /// Aadhaar number and KYC image names — those go to the partner themselves
    /// and to admins only. Never return PartnerDto to a customer.
    /// </summary>
    public class PartnerPublicDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>The customer needs this to coordinate the job.</summary>
        public string Phone { get; set; } = string.Empty;

        public string SkillCategoryName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime MemberSince { get; set; }

        public string? ProfileImageFileName { get; set; }
        public string? ProfileImageUrl =>
            string.IsNullOrWhiteSpace(ProfileImageFileName) ? null : $"/uploads/profile/{ProfileImageFileName}";

        /// <summary>Completed jobs across the platform — a simple trust signal.</summary>
        public int CompletedJobs { get; set; }

        /// <summary>Initials for the avatar placeholder when there is no photo.</summary>
        public string Initials
        {
            get
            {
                var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "?";
                return parts.Length == 1
                    ? parts[0][..1].ToUpperInvariant()
                    : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
            }
        }

        /// <summary>Requires the User and SkillCategory navigations to be Included.</summary>
        public static PartnerPublicDto From(Partner partner, int completedJobs = 0) => new()
        {
            Id = partner.Id,
            Name = partner.User?.Name ?? string.Empty,
            Phone = partner.User?.Phone ?? string.Empty,
            SkillCategoryName = partner.SkillCategory?.Name ?? string.Empty,
            IsVerified = partner.IsVerified,
            IsAvailable = partner.IsAvailable,
            MemberSince = partner.CreatedAt,
            ProfileImageFileName = partner.User?.ProfileImageFileName,
            CompletedJobs = completedJobs,
        };
    }
}
