namespace GigApp.Api.Dtos
{
    public class CartLineDto
    {
        public int ServiceItemId { get; set; }
        public string ServiceItemName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public string? ImageFileName { get; set; }
        public string? ImageUrl =>
            string.IsNullOrWhiteSpace(ImageFileName) ? null : "/uploads/service/" + ImageFileName;

        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public bool AllowsInstantBooking { get; set; }

        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class CartViewDto
    {
        public IReadOnlyList<CartLineDto> Lines { get; set; } = Array.Empty<CartLineDto>();

        public bool IsEmpty => Lines.Count == 0;
        public int ItemCount => Lines.Sum(l => l.Quantity);
        public decimal Total => Lines.Sum(l => l.LineTotal);
    }

    public class AddCartItemRequest
    {
        public int ServiceItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class SetCartQuantityRequest
    {
        public int Quantity { get; set; }
    }

    public class PlaceOrderRequest
    {
        public int AddressId { get; set; }
        public string? Note { get; set; }
        public DateTime? PreferredAt { get; set; }
    }
}
