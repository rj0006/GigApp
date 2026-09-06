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

        public UpdateProfileRequest Form { get; set; } = new();

        public IReadOnlyList<ProfileChangeDto> History { get; set; } = Array.Empty<ProfileChangeDto>();

        public BankAccountDto? BankAccount { get; set; }

        public SaveBankAccountRequest BankForm { get; set; } = new();

        public PartnerDto? Partner { get; set; }

        public AddressBookViewModel Addresses { get; set; } = new();

        public PartnerEarningsViewModel? Earnings { get; set; }
        public OrderHistoryViewModel? Orders { get; set; }
        public CustomerDashboardViewModel? Work { get; set; }
        public ServiceAreaViewModel? ServiceArea { get; set; }

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
        public PartnerEarningsViewModel? Earnings { get; set; }
        public CustomerDashboardViewModel? Work { get; set; }
        public ServiceAreaViewModel? ServiceArea { get; set; }
    }

    public class ServiceAreaViewModel
    {
        public UpdateServiceAreaRequest Form { get; set; } = new();
        public IReadOnlyList<AddressDto> Addresses { get; set; } = Array.Empty<AddressDto>();
        public bool HasPin => Form.BaseLatitude is not null && Form.BaseLongitude is not null;
        public int OpenTasksInRange { get; set; }
    }

    public class OrderHistoryViewModel
    {
        public PagedResult<GigTaskDto> Orders { get; set; } = new();
        public string? StatusFilter { get; set; }
        public string PortalSlug { get; set; } = "customer";
        public bool IsPartner => PortalSlug == "provider";

        public IReadOnlyDictionary<int, TaskRatingDto> MyRatings { get; set; } =
            new Dictionary<int, TaskRatingDto>();

        public IReadOnlyDictionary<int, SupportEnquiryDto> Enquiries { get; set; } =
            new Dictionary<int, SupportEnquiryDto>();

        public TaskRatingDto? RatingFor(int taskId) =>
            MyRatings.TryGetValue(taskId, out var rating) ? rating : null;

        public SupportEnquiryDto? EnquiryFor(int taskId) =>
            Enquiries.TryGetValue(taskId, out var enquiry) ? enquiry : null;
    }

    public class AddressBookViewModel
    {
        public IReadOnlyList<AddressDto> Addresses { get; set; } = Array.Empty<AddressDto>();

        public int WithoutCoordinates => Addresses.Count(a => !a.HasCoordinates);
    }
}
