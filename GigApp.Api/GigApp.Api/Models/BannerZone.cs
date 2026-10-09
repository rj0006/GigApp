namespace GigApp.Api.Models
{
    public class BannerZone
    {
        public int Id { get; set; }

        public int BannerId { get; set; }
        public Banner? Banner { get; set; }

        public int ServiceZoneId { get; set; }
        public ServiceZone? ServiceZone { get; set; }
    }
}
