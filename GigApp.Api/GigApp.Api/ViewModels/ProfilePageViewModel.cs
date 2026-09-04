using GigApp.Api.Dtos;

namespace GigApp.Api.ViewModels
{
    public static class ProfileSections
    {
        public const string Details = "details";
        public const string Bank = "bank";
        public const string Addresses = "addresses";
        public const string Kyc = "kyc";
        public const string Devices = "devices";
        public const string Settings = "settings";
    }

    public class ProfilePageViewModel
    {
        public UserDto User { get; set; } = new();

        public string Section { get; set; } = ProfileSections.Details;

        public string PortalSlug { get; set; } = "customer";

        public UpdateProfileRequest Form { get; set; } = new();

        public IReadOnlyList<ProfileChangeDto> History { get; set; } = Array.Empty<ProfileChangeDto>();

        public BankAccountDto? BankAccount { get; set; }

        public SaveBankAccountRequest BankForm { get; set; } = new();

        public PartnerDto? Partner { get; set; }

        public AddressBookViewModel Addresses { get; set; } = new();

        public IReadOnlyList<KycHistoryEntryDto> KycHistory { get; set; } =
            Array.Empty<KycHistoryEntryDto>();

        public bool ShowKyc => Partner is not null;

        public string SectionPath(string section) => section == ProfileSections.Details
            ? $"/{PortalSlug}/profile"
            : $"/{PortalSlug}/profile/{section}";
    }

    public class ProfileExtras
    {
        public PartnerDto? Partner { get; set; }
        public IReadOnlyList<KycHistoryEntryDto> KycHistory { get; set; } =
            Array.Empty<KycHistoryEntryDto>();
    }

    public class AddressBookViewModel
    {
        public IReadOnlyList<AddressDto> Addresses { get; set; } = Array.Empty<AddressDto>();

        public int WithoutCoordinates => Addresses.Count(a => !a.HasCoordinates);
    }
}
