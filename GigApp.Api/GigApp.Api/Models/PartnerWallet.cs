namespace GigApp.Api.Models
{
    public class PartnerWallet
    {
        public int Id { get; set; }

        public int PartnerId { get; set; }
        public Partner? Partner { get; set; }

        public decimal Balance { get; set; }
        public decimal LifetimeEarned { get; set; }
        public decimal LifetimeCommission { get; set; }
        public decimal LifetimeTax { get; set; }
        public decimal LifetimePaidOut { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
