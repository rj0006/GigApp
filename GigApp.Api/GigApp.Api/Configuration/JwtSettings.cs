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
        /// Short-lived on purpose — a refresh token (see <see cref="RefreshTokenExpiryDays"/>)
        /// is what keeps a client signed in past this without asking for a password again.
        /// </summary>
        public int ExpiryMinutes { get; set; } = 60;

        /// <summary>
        /// How long a refresh token stays redeemable with no activity. Each
        /// redemption issues a new one and revokes the one just used, so this
        /// is really "how long since the last time this device was seen."
        /// </summary>
        public int RefreshTokenExpiryDays { get; set; } = 30;
    }
}
