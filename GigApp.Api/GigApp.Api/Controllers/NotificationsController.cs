using GigApp.Api.Dtos;
using GigApp.Api.Services;
using GigApp.Api.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    // Same data the bell has always shown, as JSON — the live update after a
    // SignalR "notification" push re-calls this, and it is what a mobile
    // client's own bell will call too, since there is nowhere else this data
    // comes from.
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notifier;

        public NotificationsController(INotificationService notifier) => _notifier = notifier;

        [HttpGet("summary")]
        public async Task<ActionResult<NotificationSummaryDto>> Summary(CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();

            return Ok(new NotificationSummaryDto
            {
                UnreadCount = await _notifier.UnreadCountAsync(userId, ct),
                Recent = await _notifier.RecentAsync(userId, 6, ct),
            });
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<NotificationDto>>> List(
            [FromQuery] PageRequest paging, CancellationToken ct) =>
            Ok(await _notifier.ListAsync(User.GetRequiredUserId(), paging, ct));

        [HttpPost("read")]
        public async Task<IActionResult> MarkRead([FromBody] MarkNotificationsReadRequest? request, CancellationToken ct)
        {
            await _notifier.MarkReadAsync(User.GetRequiredUserId(), request?.NotificationId, ct);
            return NoContent();
        }
    }

    public class MarkNotificationsReadRequest
    {
        public int? NotificationId { get; set; }
    }
}
