using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class AddressDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? HouseNumber { get; set; }
        public string Line1 { get; set; } = string.Empty;
        public string? Line2 { get; set; }
        public string? Landmark { get; set; }
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }
        public string Pincode { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsDefault { get; set; }

        public bool HasCoordinates => Latitude is not null && Longitude is not null;
        public string SingleLine { get; set; } = string.Empty;

        public static AddressDto From(Address address) => new()
        {
            Id = address.Id,
            Label = address.Label,
            HouseNumber = address.HouseNumber,
            Line1 = address.Line1,
            Line2 = address.Line2,
            Landmark = address.Landmark,
            City = address.City,
            State = address.State,
            Pincode = address.Pincode,
            Latitude = address.Latitude,
            Longitude = address.Longitude,
            IsDefault = address.IsDefault,
            SingleLine = address.ToSingleLine(),
        };
    }

    public class SaveAddressRequest
    {
        [Required, StringLength(20)]
        [Display(Name = "Label")]
        public string Label { get; set; } = AddressLabel.Home;

        [StringLength(50)]
        [Display(Name = "Flat or house number")]
        public string? HouseNumber { get; set; }

        [Required(ErrorMessage = "Enter the address.")]
        [StringLength(200, MinimumLength = 3)]
        [Display(Name = "Address")]
        public string Line1 { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Area or street")]
        public string? Line2 { get; set; }

        [StringLength(150)]
        [Display(Name = "Landmark")]
        public string? Landmark { get; set; }

        [Required(ErrorMessage = "Enter the city.")]
        [StringLength(80)]
        public string City { get; set; } = string.Empty;

        [StringLength(80)]
        public string? State { get; set; }

        [Required(ErrorMessage = "Enter the pincode.")]
        [RegularExpression(ValidationPatterns.Pincode, ErrorMessage = ValidationPatterns.PincodeMessage)]
        public string Pincode { get; set; } = string.Empty;

        /// <summary>
        /// Optional. Sent by the browser or app when the user shares their
        /// location; without it this address cannot be used for distance
        /// matching, but it can still be saved and typed out.
        /// </summary>
        [Range(-90, 90)]
        public double? Latitude { get; set; }

        [Range(-180, 180)]
        public double? Longitude { get; set; }

        [Display(Name = "Use as my default address")]
        public bool IsDefault { get; set; }
    }

    public class UpdateServiceAreaRequest
    {
        [Range(-90, 90)]
        public double? BaseLatitude { get; set; }

        [Range(-180, 180)]
        public double? BaseLongitude { get; set; }

        [Range(1, 100, ErrorMessage = "Choose a radius between 1 and 100 km.")]
        [Display(Name = "How far will you travel? (km)")]
        public int ServiceRadiusKm { get; set; } = 10;

        [StringLength(80)]
        [Display(Name = "Base city")]
        public string? BaseCity { get; set; }

        [RegularExpression(ValidationPatterns.Pincode, ErrorMessage = ValidationPatterns.PincodeMessage)]
        [Display(Name = "Base pincode")]
        public string? BasePincode { get; set; }
    }

    public class AddressResult
    {
        public bool Succeeded { get; init; }
        public string? Error { get; init; }
        public AddressDto? Address { get; init; }

        public static AddressResult Ok(AddressDto address) => new() { Succeeded = true, Address = address };
        public static AddressResult Fail(string error) => new() { Succeeded = false, Error = error };
    }
}
