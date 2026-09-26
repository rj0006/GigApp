namespace GigApp.Api.Models
{
    public class AuthSettings
    {
        public int Id { get; set; }
        public string CustomerLoginMode { get; set; } = LoginMode.Otp;
        public string PartnerLoginMode { get; set; } = LoginMode.Otp;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class LoginMode
    {
        public const string Password = "password";
        public const string Otp = "otp";

        public static readonly string[] All = { Password, Otp };

        public static bool IsValid(string? mode) => mode is not null && All.Contains(mode);
    }
}
