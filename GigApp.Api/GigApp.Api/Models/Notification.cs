namespace GigApp.Api.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public string Type { get; set; } = NotificationTypes.General;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        /// <summary>Where clicking it should go. Always a local path.</summary>
        public string? Link { get; set; }

        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class NotificationTypes
    {
        public const string General = "general";
        public const string JobOffered = "job_offered";
        public const string JobAssigned = "job_assigned";
        public const string JobStarted = "job_started";
        public const string JobCompleted = "job_completed";
        public const string BidReceived = "bid_received";
        public const string BidCountered = "bid_countered";
        public const string BidRejected = "bid_rejected";
        public const string KycReviewed = "kyc_reviewed";
        public const string EnquiryAnswered = "enquiry_answered";
        public const string NobodyAvailable = "nobody_available";
        public const string PartnerCancelled = "partner_cancelled";

        public static readonly string[] All =
        {
            General, JobOffered, JobAssigned, JobStarted, JobCompleted,
            BidReceived, BidCountered, BidRejected,
            KycReviewed, EnquiryAnswered, NobodyAvailable, PartnerCancelled,
        };

        public static bool IsValid(string? type) => type is not null && All.Contains(type);

        public static string Icon(string type) => type switch
        {
            JobOffered => "\u26A1",
            JobAssigned => "\u2713",
            JobStarted => "\u25B6",
            JobCompleted => "\u2605",
            BidReceived => "\u20B9",
            BidCountered => "\u21C4",
            BidRejected => "\u2715",
            KycReviewed => "\U0001F4C4",
            EnquiryAnswered => "\u260E",
            NobodyAvailable => "\u26A0",
            PartnerCancelled => "\u21A9",
            _ => "\U0001F514",
        };
    }
}
