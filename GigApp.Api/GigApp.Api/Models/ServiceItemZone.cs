namespace GigApp.Api.Models
{
    public class ServiceItemZone
    {
        public int Id { get; set; }

        public int ServiceItemId { get; set; }
        public ServiceItem? ServiceItem { get; set; }

        public int ServiceZoneId { get; set; }
        public ServiceZone? ServiceZone { get; set; }
    }
}
