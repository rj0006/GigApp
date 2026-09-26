using GigApp.Api.Services.Realtime;

namespace GigApp.Api.Services.Notifications
{
    // Every call to INotificationService.PushAsync reaches this automatically
    // — nothing that raises a notification anywhere in the app had to change
    // for the bell to update live instead of on the next page load.
    public class SignalRNotificationChannel : INotificationChannel
    {
        private readonly IRealtimeNotifier _realtime;

        public SignalRNotificationChannel(IRealtimeNotifier realtime) => _realtime = realtime;

        public string Name => "signalr";

        public Task SendAsync(NotificationRequest request, CancellationToken ct = default) =>
            _realtime.PushNotificationAsync(request.UserId, request.Title, request.Body, request.Link, ct);
    }
}
