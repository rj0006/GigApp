namespace GigApp.Api.Models
{
    public class OtpChallenge
    {
        public int Id { get; set; }

        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;

        public int Attempts { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public const int MaxAttempts = 5;
        public const int ValidityMinutes = 5;

        public bool IsLive(DateTime now) => ConsumedAt is null && ExpiresAt > now && Attempts < MaxAttempts;
    }
}
