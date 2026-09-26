namespace GigApp.Api.Services.Otp
{
    public interface IOtpSender
    {
        Task SendAsync(string phone, string code, CancellationToken ct = default);
    }

    public class LoggingOtpSender : IOtpSender
    {
        private readonly ILogger<LoggingOtpSender> _logger;

        public LoggingOtpSender(ILogger<LoggingOtpSender> logger) => _logger = logger;

        public Task SendAsync(string phone, string code, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "OTP {Code} for {Phone} — no SMS provider configured yet, logging instead of sending",
                code, phone);

            return Task.CompletedTask;
        }
    }
}
