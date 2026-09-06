using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Notifications
{
    public record NotificationRequest(
        int UserId, string Type, string Title, string Body, string? Link = null);

    /// <summary>
    /// A way of getting a notification in front of somebody. In-app is the one
    /// that exists; SMS and push are registered alongside it once a provider is
    /// in place, and nothing that raises a notification has to change.
    /// </summary>
    public interface INotificationChannel
    {
        string Name { get; }

        Task SendAsync(NotificationRequest request, CancellationToken ct = default);
    }

    public interface INotificationService
    {
        Task PushAsync(NotificationRequest request, CancellationToken ct = default);

        Task PushManyAsync(
            IEnumerable<NotificationRequest> requests, CancellationToken ct = default);

        Task<int> UnreadCountAsync(int userId, CancellationToken ct = default);

        Task<PagedResult<NotificationDto>> ListAsync(
            int userId, PageRequest paging, CancellationToken ct = default);

        Task<IReadOnlyList<NotificationDto>> RecentAsync(
            int userId, int take = 6, CancellationToken ct = default);

        Task MarkReadAsync(int userId, int? notificationId, CancellationToken ct = default);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly IEnumerable<INotificationChannel> _channels;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext context,
            IEnumerable<INotificationChannel> channels,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _channels = channels;
            _logger = logger;
        }

        public Task PushAsync(NotificationRequest request, CancellationToken ct = default) =>
            PushManyAsync(new[] { request }, ct);

        public async Task PushManyAsync(
            IEnumerable<NotificationRequest> requests, CancellationToken ct = default)
        {
            var list = requests.Where(r => r.UserId > 0).ToList();
            if (list.Count == 0) return;

            _context.Notifications.AddRange(list.Select(r => new Notification
            {
                UserId = r.UserId,
                Type = NotificationTypes.IsValid(r.Type) ? r.Type : NotificationTypes.General,
                Title = r.Title,
                Body = r.Body,
                Link = r.Link,
                CreatedAt = DateTime.UtcNow,
            }));

            await _context.SaveChangesAsync(ct);

            foreach (var channel in _channels)
            {
                foreach (var request in list)
                {
                    try
                    {
                        await channel.SendAsync(request, ct);
                    }
                    catch (Exception ex)
                    {
                        // A channel that is down must not roll back the job it
                        // was telling somebody about.
                        _logger.LogWarning(ex,
                            "Notification channel {Channel} failed for user {UserId}",
                            channel.Name, request.UserId);
                    }
                }
            }
        }

        public Task<int> UnreadCountAsync(int userId, CancellationToken ct = default) =>
            _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        public async Task<PagedResult<NotificationDto>> ListAsync(
            int userId, PageRequest paging, CancellationToken ct = default)
        {
            var page = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return page.Map(NotificationDto.From);
        }

        public async Task<IReadOnlyList<NotificationDto>> RecentAsync(
            int userId, int take = 6, CancellationToken ct = default) =>
            await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => NotificationDto.From(n))
                .ToListAsync(ct);

        public async Task MarkReadAsync(
            int userId, int? notificationId, CancellationToken ct = default)
        {
            var unread = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead);

            if (notificationId is not null)
                unread = unread.Where(n => n.Id == notificationId);

            await unread.ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
        }
    }
}
