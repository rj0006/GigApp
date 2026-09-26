namespace GigApp.Api.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }
        public User? Customer { get; set; }

        public int ServiceItemId { get; set; }
        public ServiceItem? ServiceItem { get; set; }

        public int Quantity { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
