namespace GigApp.Api.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public string TokenHash { get; set; } = string.Empty;

        public string? DeviceLabel { get; set; }
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
    }
}
