namespace GigApp.Api.Models
{
    /// <summary>
    /// Promotional artwork on the storefront. A master rather than markup, so a
    /// campaign is a row an administrator adds rather than a deployment.
    /// </summary>
    public class Banner : IHasImage
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? CallToAction { get; set; }

        public string? ImageFileName { get; set; }

        /// <summary>Where clicking it goes. Local paths only — checked on save.</summary>
        public string? LinkUrl { get; set; }

        public string Placement { get; set; } = BannerPlacements.Spotlight;

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool IsLive(DateTime now) =>
            IsActive && (StartsAt is null || StartsAt <= now) && (EndsAt is null || EndsAt >= now);
    }

    public static class BannerPlacements
    {
        /// <summary>The row of small cards under the hero.</summary>
        public const string Spotlight = "spotlight";

        /// <summary>One full-width strip between sections.</summary>
        public const string Wide = "wide";

        public static readonly string[] All = { Spotlight, Wide };

        public static bool IsValid(string? placement) =>
            placement is not null && All.Contains(placement);

        public static string Label(string placement) => placement switch
        {
            Spotlight => "In the spotlight",
            Wide => "Full-width strip",
            _ => placement,
        };
    }
}
