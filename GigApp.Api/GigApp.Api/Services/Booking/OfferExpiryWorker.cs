using GigApp.Api.Configuration;
using Microsoft.Extensions.Options;

namespace GigApp.Api.Services.Booking
{
    /// <summary>
    /// Rolls the offer chain forward. An offer nobody answers has to expire on
    /// its own, or a partner who closed the tab would hold a customer's booking
    /// indefinitely. Its own scope per pass, because a hosted service outlives
    /// any request scope.
    /// </summary>
    public class OfferExpiryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly PlatformOptions _platform;
        private readonly ILogger<OfferExpiryWorker> _logger;

        public OfferExpiryWorker(
            IServiceScopeFactory scopes,
            IOptions<PlatformOptions> platform,
            ILogger<OfferExpiryWorker> logger)
        {
            _scopes = scopes;
            _platform = platform.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_platform.AutoAssignInstant)
            {
                _logger.LogInformation("Automatic assignment is off; the offer worker is idle.");
                return;
            }

            var interval = TimeSpan.FromSeconds(Math.Max(10, _platform.OfferSweepSeconds));

            using var timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var offers = scope.ServiceProvider.GetRequiredService<IOfferService>();

                    var expired = await offers.ExpireDueAsync(stoppingToken);

                    if (expired > 0)
                        _logger.LogInformation("Expired {Count} offer(s) and moved them on", expired);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // One bad pass must not stop the loop, or every later offer
                    // would hang for the life of the process.
                    _logger.LogError(ex, "The offer sweep failed; it will run again shortly");
                }
            }
        }
    }
}
