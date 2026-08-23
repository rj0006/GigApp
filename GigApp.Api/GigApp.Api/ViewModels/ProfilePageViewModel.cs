using GigApp.Api.Dtos;

namespace GigApp.Api.ViewModels
{
    public class ProfilePageViewModel
    {
        public UserDto User { get; set; } = new();

        /// <summary>Pre-filled with the current values.</summary>
        public UpdateProfileRequest Form { get; set; } = new();

        /// <summary>Newest first, rebuilt from the audit trail.</summary>
        public IReadOnlyList<ProfileChangeDto> History { get; set; } = Array.Empty<ProfileChangeDto>();
    }

    public class AddressBookViewModel
    {
        public IReadOnlyList<AddressDto> Addresses { get; set; } = Array.Empty<AddressDto>();

        /// <summary>Addresses without a pin cannot be used for distance matching.</summary>
        public int WithoutCoordinates => Addresses.Count(a => !a.HasCoordinates);
    }
}
