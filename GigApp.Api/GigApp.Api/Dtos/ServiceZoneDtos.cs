using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class ServiceZoneOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ServiceZoneDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int RadiusKm { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public IReadOnlyList<int> CategoryIds { get; set; } = Array.Empty<int>();

        public static ServiceZoneDto From(ServiceZone zone) => new()
        {
            Id = zone.Id,
            Name = zone.Name,
            Latitude = zone.Center?.Y,
            Longitude = zone.Center?.X,
            RadiusKm = zone.RadiusKm,
            IsActive = zone.IsActive,
            DisplayOrder = zone.DisplayOrder,
            CreatedAt = zone.CreatedAt,
            UpdatedAt = zone.UpdatedAt,
            CategoryIds = zone.Categories.Select(c => c.SkillCategoryId).ToList(),
        };
    }

    public class NearestZoneDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double DistanceKm { get; set; }
    }

    public class SaveServiceZoneRequest
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Enter a zone name.")]
        [StringLength(80, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the centre latitude.")]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Enter the centre longitude.")]
        [Range(-180, 180)]
        public double Longitude { get; set; }

        [Range(1, 200, ErrorMessage = "Radius must be between 1 and 200 km.")]
        public int RadiusKm { get; set; } = 10;

        public bool IsActive { get; set; } = true;

        [Range(0, 9999)]
        public int DisplayOrder { get; set; }

        public List<int> CategoryIds { get; set; } = new();
    }
}
