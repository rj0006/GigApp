using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = NotificationTypes.General;
        public string Icon => NotificationTypes.Icon(Type);
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Link { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string Age
        {
            get
            {
                var span = DateTime.UtcNow - CreatedAt;

                return span switch
                {
                    { TotalMinutes: < 1 } => "just now",
                    { TotalMinutes: < 60 } => $"{(int)span.TotalMinutes} min ago",
                    { TotalHours: < 24 } => $"{(int)span.TotalHours} h ago",
                    { TotalDays: < 7 } => $"{(int)span.TotalDays} d ago",
                    _ => CreatedAt.ToString("dd MMM"),
                };
            }
        }

        public static NotificationDto From(Notification notification) => new()
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Body = notification.Body,
            Link = notification.Link,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
        };
    }

    public class NotificationSummaryDto
    {
        public int UnreadCount { get; set; }
        public IReadOnlyList<NotificationDto> Recent { get; set; } = Array.Empty<NotificationDto>();
    }
}
