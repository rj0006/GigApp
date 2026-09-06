using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class BannerDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? CallToAction { get; set; }

        public string? ImageFileName { get; set; }
        public string? ImageUrl =>
            string.IsNullOrWhiteSpace(ImageFileName) ? null : "/uploads/banner/" + ImageFileName;

        public string? LinkUrl { get; set; }
        public string Placement { get; set; } = BannerPlacements.Spotlight;
        public string PlacementLabel => BannerPlacements.Label(Placement);

        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public bool IsLive => IsActive
            && (StartsAt is null || StartsAt <= DateTime.UtcNow)
            && (EndsAt is null || EndsAt >= DateTime.UtcNow);

        public string Window => (StartsAt, EndsAt) switch
        {
            (null, null) => "Always",
            (not null, null) => $"From {StartsAt:dd MMM yyyy}",
            (null, not null) => $"Until {EndsAt:dd MMM yyyy}",
            _ => $"{StartsAt:dd MMM} to {EndsAt:dd MMM yyyy}",
        };

        public static BannerDto From(Banner banner) => new()
        {
            Id = banner.Id,
            Title = banner.Title,
            Subtitle = banner.Subtitle,
            CallToAction = banner.CallToAction,
            ImageFileName = banner.ImageFileName,
            LinkUrl = banner.LinkUrl,
            Placement = banner.Placement,
            SortOrder = banner.SortOrder,
            IsActive = banner.IsActive,
            StartsAt = banner.StartsAt,
            EndsAt = banner.EndsAt,
        };
    }

    public class SaveBannerRequest
    {
        [Required(ErrorMessage = "Enter a headline.")]
        [StringLength(120, MinimumLength = 2)]
        [Display(Name = "Headline")]
        public string Title { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Supporting line")]
        public string? Subtitle { get; set; }

        [StringLength(40)]
        [Display(Name = "Button text")]
        public string? CallToAction { get; set; }

        [StringLength(300)]
        [Display(Name = "Links to")]
        public string? LinkUrl { get; set; }

        [Required]
        [Display(Name = "Placement")]
        public string Placement { get; set; } = BannerPlacements.Spotlight;

        [Range(0, 9999)]
        [Display(Name = "Display order")]
        public int SortOrder { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Runs from")]
        public DateTime? StartsAt { get; set; }

        [Display(Name = "Runs until")]
        public DateTime? EndsAt { get; set; }

        [Display(Name = "Banner image")]
        public IFormFile? Image { get; set; }
    }
}
