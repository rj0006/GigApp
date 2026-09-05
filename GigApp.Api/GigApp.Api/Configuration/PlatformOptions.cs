namespace GigApp.Api.Configuration
{
    public class PlatformOptions
    {
        public const string SectionName = "Platform";

        public string CountryCode { get; set; } = "IN";
        public string Currency { get; set; } = "INR";
    }
}
