namespace GigApp.Api.Models
{
    public class SupportEnquiry
    {
        public int Id { get; set; }

        public int GigTaskId { get; set; }
        public GigTask? GigTask { get; set; }

        public int RaisedByUserId { get; set; }
        public User? RaisedByUser { get; set; }

        public string RaisedByRole { get; set; } = string.Empty;

        public string Topic { get; set; } = EnquiryTopics.Other;
        public string Message { get; set; } = string.Empty;

        public string Status { get; set; } = EnquiryStatus.Open;

        public string? Resolution { get; set; }
        public int? ResolvedByUserId { get; set; }
        public User? ResolvedByUser { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public static class EnquiryStatus
    {
        public const string Open = "open";
        public const string InProgress = "in_progress";
        public const string Resolved = "resolved";

        public static readonly string[] All = { Open, InProgress, Resolved };

        public static readonly string[] Live = { Open, InProgress };

        public static bool IsValid(string? status) => status is not null && All.Contains(status);

        public static bool IsLive(string status) => Live.Contains(status);

        public static string Label(string status) => status switch
        {
            Open => "Open",
            InProgress => "Being looked at",
            Resolved => "Resolved",
            _ => status,
        };

        public static string BadgeClass(string status) => status switch
        {
            Open => "text-bg-danger",
            InProgress => "text-bg-warning",
            Resolved => "text-bg-success",
            _ => "text-bg-secondary",
        };
    }

    public static class EnquiryTopics
    {
        public const string Payment = "payment";
        public const string Quality = "quality";
        public const string Behaviour = "behaviour";
        public const string Timing = "timing";
        public const string Other = "other";

        public static readonly string[] All = { Payment, Quality, Behaviour, Timing, Other };

        public static bool IsValid(string? topic) => topic is not null && All.Contains(topic);

        public static string Label(string topic) => topic switch
        {
            Payment => "Payment or amount",
            Quality => "Quality of work",
            Behaviour => "Behaviour",
            Timing => "Timing or no show",
            Other => "Something else",
            _ => topic,
        };
    }
}
