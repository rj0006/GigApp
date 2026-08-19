namespace GigApp.Api.Configuration
{
    public class JwtSettings
    {
        public const string SectionName = "Jwt";

        /// <summary>
        /// HMAC-SHA256 signing key. Must be at least 32 bytes. Supplied per
        /// environment — user-secrets in development, an environment variable
        /// or key vault in production. Never commit a production key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        public string Issuer { get; set; } = "GigApp.Api";
        public string Audience { get; set; } = "GigApp.Clients";

        /// <summary>
        /// Long-lived by default because mobile clients cannot silently
        /// re-authenticate yet. Shorten this once refresh tokens exist.
        /// </summary>
        public int ExpiryMinutes { get; set; } = 60 * 24 * 7; // 7 days
    }
}
