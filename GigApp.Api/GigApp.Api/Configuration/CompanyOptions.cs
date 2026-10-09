namespace GigApp.Api.Configuration
{
    public class CompanyOptions
    {
        public const string SectionName = "Company";

        public string Address { get; set; } = "";
        public string HelplineNumber { get; set; } = "";
        public string HelplineHours { get; set; } = "";
        public string FacebookUrl { get; set; } = "";
        public string InstagramUrl { get; set; } = "";
        public string TwitterUrl { get; set; } = "";
        public string LinkedInUrl { get; set; } = "";
        public string YouTubeUrl { get; set; } = "";
    }
}
