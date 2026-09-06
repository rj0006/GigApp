namespace GigApp.Api.Models
{
    /// <summary>
    /// A fixed-price job held for one partner for a short window. It is first
    /// refusal, not a lock — once it expires the job opens to everyone, so a
    /// silent partner can never strand a customer.
    /// </summary>
    public class TaskOffer
    {
        public int Id { get; set; }

        public int GigTaskId { get; set; }
        public GigTask? GigTask { get; set; }

        public int PartnerId { get; set; }
        public Partner? Partner { get; set; }

        /// <summary>Position in the ranking when this offer was made, from one.</summary>
        public int Rank { get; set; }

        public string Status { get; set; } = OfferStatus.Pending;

        public DateTime OfferedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RespondedAt { get; set; }

        public bool IsLive(DateTime now) => Status == OfferStatus.Pending && ExpiresAt > now;
    }

    public static class OfferStatus
    {
        public const string Pending = "pending";
        public const string Accepted = "accepted";
        public const string Declined = "declined";
        public const string Expired = "expired";

        public static readonly string[] All = { Pending, Accepted, Declined, Expired };

        public static bool IsValid(string? status) => status is not null && All.Contains(status);
    }
}
