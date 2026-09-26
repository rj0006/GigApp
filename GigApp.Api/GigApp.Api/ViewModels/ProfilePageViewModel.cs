using GigApp.Api.Dtos;

namespace GigApp.Api.ViewModels
{
    public static class ProfileSections
    {
        public const string Details = "details";
        public const string Bank = "bank";
        public const string Addresses = "addresses";
        public const string Earnings = "earnings";
        public const string Orders = "orders";
        public const string PostTask = "post";
        public const string Tasks = "tasks";
        public const string ServiceArea = "area";
        public const string Kyc = "kyc";
        public const string Devices = "devices";
        public const string Settings = "settings";
    }

    public class ProfilePageViewModel
    {
        public UserDto User { get; set; } = new();

        public string Section { get; set; } = ProfileSections.Details;

        public string PortalSlug { get; set; } = "customer";

        public PartnerDto? Partner { get; set; }

        public bool ShowKyc => Partner is not null;

        public string SectionPath(string section) => section == ProfileSections.Details
            ? $"/{PortalSlug}/profile"
            : $"/{PortalSlug}/profile/{section}";
    }

    public class ProfileExtras
    {
        public PartnerDto? Partner { get; set; }
    }
}
