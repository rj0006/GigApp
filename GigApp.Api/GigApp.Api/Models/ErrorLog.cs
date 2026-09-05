namespace GigApp.Api.Models
{
    public class ErrorLog
    {
        public long Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? InnerMessage { get; set; }

        public string? Module { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestMethod { get; set; }

        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; }
        public string? ResolutionNote { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
