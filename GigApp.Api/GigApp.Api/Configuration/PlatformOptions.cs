namespace GigApp.Api.Configuration
{
    public class PlatformOptions
    {
        public const string SectionName = "Platform";

        public string CountryCode { get; set; } = "IN";
        public string Currency { get; set; } = "INR";

        /// <summary>
        /// Offer a fixed-price job to the best-ranked partner instead of leaving
        /// it for whoever claims it first. Turning this off falls back to that.
        /// </summary>
        public bool AutoAssignInstant { get; set; } = true;

        /// <summary>
        /// How long one partner holds first refusal. Five minutes suits in-app
        /// notification; shorten it once a job reaches a phone as a push.
        /// </summary>
        public int OfferWindowSeconds { get; set; } = 300;

        /// <summary>How often expired offers are swept and moved on.</summary>
        public int OfferSweepSeconds { get; set; } = 30;
    }
}
