namespace GigApp.Api.Services.Otp
{
    public static class OtpCredentials
    {
        public static string GenerateRandomPassword() => $"{Guid.NewGuid():N}Aa1!";
    }
}
