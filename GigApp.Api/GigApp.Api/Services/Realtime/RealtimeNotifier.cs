using GigApp.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GigApp.Api.Services.Realtime
{
    // The live half of a change — a topic string, never the data itself. A
    // client that cares about that topic just re-calls the same fetch it
    // already uses on page load; this only tells it when to bother.
    public interface IRealtimeNotifier
    {
        Task NotifyUserAsync(int userId, string topic, CancellationToken ct = default);

        Task NotifyCategoryPartnersAsync(int categoryId, string topic, CancellationToken ct = default);

        Task NotifyAdminsAsync(string topic, CancellationToken ct = default);

        Task PushNotificationAsync(
            int userId, string title, string body, string? link, CancellationToken ct = default);
    }

    public class RealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<AppHub> _hub;

        public RealtimeNotifier(IHubContext<AppHub> hub) => _hub = hub;

        public Task NotifyUserAsync(int userId, string topic, CancellationToken ct = default) =>
            _hub.Clients.Group(RealtimeGroups.User(userId)).SendAsync("refresh", topic, ct);

        public Task NotifyCategoryPartnersAsync(int categoryId, string topic, CancellationToken ct = default) =>
            _hub.Clients.Group(RealtimeGroups.CategoryPartners(categoryId)).SendAsync("refresh", topic, ct);

        public Task NotifyAdminsAsync(string topic, CancellationToken ct = default) =>
            _hub.Clients.Group(RealtimeGroups.Admins).SendAsync("refresh", topic, ct);

        public Task PushNotificationAsync(
            int userId, string title, string body, string? link, CancellationToken ct = default) =>
            _hub.Clients.Group(RealtimeGroups.User(userId))
                .SendAsync("notification", new { title, body, link }, ct);
    }
}
