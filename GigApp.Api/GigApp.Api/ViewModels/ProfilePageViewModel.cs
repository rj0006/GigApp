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
}
