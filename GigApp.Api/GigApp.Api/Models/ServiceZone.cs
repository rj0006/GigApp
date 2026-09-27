using NetTopologySuite.Geometries;

namespace GigApp.Api.Models
{
    // A circle — centre plus radius — the storefront sells inside; a customer resolves to the nearest one their point falls inside, or none.
    public class ServiceZone
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Point? Center { get; set; }

        public int RadiusKm { get; set; } = 10;

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ServiceZoneCategory> Categories { get; set; } = new List<ServiceZoneCategory>();
    }
}
