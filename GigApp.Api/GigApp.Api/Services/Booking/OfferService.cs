using GigApp.Api.Configuration;
using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services.Geo;
using GigApp.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GigApp.Api.Services.Booking
{
    public record OfferResult(bool Succeeded, string? Error)
    {
        public static OfferResult Ok() => new(true, null);
        public static OfferResult Fail(string error) => new(false, error);
    }

    /// <summary>
    /// Automatic assignment. A fixed-price job is offered to the best-ranked
    /// partner for a short window; if they decline or stay silent it moves to
    /// the next, and when the list runs out the job opens to everyone rather
    /// than sitting unassigned.
    /// </summary>
    public interface IOfferService
    {
        Task<OfferResult> StartAsync(int taskId, CancellationToken ct = default);

        Task<OfferResult> RespondAsync(
            int partnerUserId, int offerId, bool accepted, CancellationToken ct = default);

        Task<TaskOfferDto?> LiveOfferForPartnerAsync(
            int partnerId, CancellationToken ct = default);

        Task<TaskOffer?> LiveOfferForTaskAsync(int taskId, CancellationToken ct = default);

        Task<int> ExpireDueAsync(CancellationToken ct = default);
    }

    public class OfferService : IOfferService
    {
        private readonly AppDbContext _context;
        private readonly IMatchService _match;
        private readonly INotificationService _notifications;
        private readonly ITaskClaimService _claims;
        private readonly PlatformOptions _platform;
        private readonly ILogger<OfferService> _logger;

        public OfferService(
            AppDbContext context,
            IMatchService match,
            INotificationService notifications,
            ITaskClaimService claims,
            IOptions<PlatformOptions> platform,
            ILogger<OfferService> logger)
        {
            _context = context;
            _match = match;
            _notifications = notifications;
            _claims = claims;
            _platform = platform.Value;
            _logger = logger;
        }

        public async Task<OfferResult> StartAsync(int taskId, CancellationToken ct = default)
        {
            if (!_platform.AutoAssignInstant) return OfferResult.Fail("Automatic assignment is off.");

            var task = await _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Customer)
                .Include(t => t.ServiceItem)
                .FirstOrDefaultAsync(t => t.Id == taskId, ct);

            if (task is null) return OfferResult.Fail("Task not found.");

            // Bidding work is the customer's choice to make. Only a fixed price
            // has a single right answer to offer.
            if (task.BookingMode != TaskBookingMode.Instant)
                return OfferResult.Fail("Only a fixed-price job is offered automatically.");

            if (task.Status != GigTaskStatus.Pending)
                return OfferResult.Fail("This job is no longer waiting for a partner.");

            var live = await LiveOfferForTaskAsync(taskId, ct);
            if (live is not null) return OfferResult.Fail("This job is already with a partner.");

            var alreadyOffered = await _context.TaskOffers
                .Where(o => o.GigTaskId == taskId)
                .Select(o => o.PartnerId)
                .ToListAsync(ct);

            var cancelledBy = await _context.TaskCancellations
                .Where(c => c.GigTaskId == taskId)
                .Select(c => c.PartnerId)
                .ToListAsync(ct);

            var excluded = alreadyOffered.Concat(cancelledBy).Distinct().ToList();

            var ranked = await _match.RankPartnersAsync(taskId, 30, ct);

            var next = ranked.FirstOrDefault(p =>
                !excluded.Contains(p.PartnerId) && p.IsAvailable && !p.IsOutOfRange);

            if (next is null)
            {
                await OpenToEveryoneAsync(task, alreadyOffered.Count, ct);
                return OfferResult.Fail("No partner is left to offer this to.");
            }

            var offer = new TaskOffer
            {
                GigTaskId = taskId,
                PartnerId = next.PartnerId,
                Rank = alreadyOffered.Count + 1,
                Status = OfferStatus.Pending,
                OfferedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(_platform.OfferWindowSeconds),
            };

            _context.TaskOffers.Add(offer);
            await _context.SaveChangesAsync(ct);

            var partnerUserId = await _context.Partners
                .Where(p => p.Id == next.PartnerId)
                .Select(p => p.UserId)
                .FirstAsync(ct);

            var minutes = Math.Max(1, _platform.OfferWindowSeconds / 60);

            await _notifications.PushAsync(new NotificationRequest(
                partnerUserId,
                NotificationTypes.JobOffered,
                $"New job for you — ₹{task.AgreedAmount ?? task.Budget:N0}",
                $"{task.ServiceItem?.Name ?? "A job"} at {task.Address}. "
                    + $"You have {minutes} minute(s) to accept before it goes to the next partner.",
                "/provider"), ct);

            _logger.LogInformation(
                "Task {TaskId} offered to partner {PartnerId} at rank {Rank}",
                taskId, next.PartnerId, offer.Rank);

            return OfferResult.Ok();
        }

        public async Task<OfferResult> RespondAsync(
            int partnerUserId, int offerId, bool accepted, CancellationToken ct = default)
        {
            var offer = await _context.TaskOffers
                .Include(o => o.Partner)
                .FirstOrDefaultAsync(o => o.Id == offerId, ct);

            if (offer is null) return OfferResult.Fail("Offer not found.");

            if (offer.Partner?.UserId != partnerUserId)
                return OfferResult.Fail("This offer is not yours.");

            if (offer.Status != OfferStatus.Pending)
                return OfferResult.Fail("You have already answered this offer.");

            if (offer.ExpiresAt <= DateTime.UtcNow)
            {
                offer.Status = OfferStatus.Expired;
                offer.RespondedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                return OfferResult.Fail("That offer ran out. It has gone to another partner.");
            }

            offer.RespondedAt = DateTime.UtcNow;

            if (!accepted)
            {
                offer.Status = OfferStatus.Declined;
                await _context.SaveChangesAsync(ct);

                await StartAsync(offer.GigTaskId, ct);
                return OfferResult.Ok();
            }

            var claim = await _claims.ClaimAsync(partnerUserId, offer.GigTaskId, ct);

            if (!claim.Succeeded)
            {
                offer.Status = OfferStatus.Expired;
                await _context.SaveChangesAsync(ct);
                return OfferResult.Fail(claim.Error ?? "That job could not be taken.");
            }

            offer.Status = OfferStatus.Accepted;
            await _context.SaveChangesAsync(ct);

            return OfferResult.Ok();
        }

        public async Task<TaskOfferDto?> LiveOfferForPartnerAsync(
            int partnerId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            var offer = await _context.TaskOffers
                .AsNoTracking()
                .Include(o => o.GigTask)!.ThenInclude(t => t!.Category)
                .Include(o => o.GigTask)!.ThenInclude(t => t!.ServiceItem)
                .Include(o => o.GigTask)!.ThenInclude(t => t!.Customer)
                .Where(o => o.PartnerId == partnerId
                         && o.Status == OfferStatus.Pending
                         && o.ExpiresAt > now
                         && o.GigTask!.Status == GigTaskStatus.Pending)
                .OrderBy(o => o.ExpiresAt)
                .FirstOrDefaultAsync(ct);

            return offer is null ? null : TaskOfferDto.From(offer);
        }

        public Task<TaskOffer?> LiveOfferForTaskAsync(int taskId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            return _context.TaskOffers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.GigTaskId == taskId
                                       && o.Status == OfferStatus.Pending
                                       && o.ExpiresAt > now, ct);
        }

        public async Task<int> ExpireDueAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            var due = await _context.TaskOffers
                .Where(o => o.Status == OfferStatus.Pending && o.ExpiresAt <= now)
                .Select(o => new { o.Id, o.GigTaskId })
                .ToListAsync(ct);

            if (due.Count == 0) return 0;

            await _context.TaskOffers
                .Where(o => o.Status == OfferStatus.Pending && o.ExpiresAt <= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.Status, OfferStatus.Expired)
                    .SetProperty(o => o.RespondedAt, now), ct);

            foreach (var taskId in due.Select(d => d.GigTaskId).Distinct())
                await StartAsync(taskId, ct);

            return due.Count;
        }

        private async Task OpenToEveryoneAsync(GigTask task, int tried, CancellationToken ct)
        {
            _logger.LogInformation(
                "Task {TaskId} exhausted its offer chain after {Tried} partner(s)", task.Id, tried);

            var admins = await _context.Users
                .Where(u => UserRoles.AdminRoles.Contains(u.Role) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);

            var body = tried == 0
                ? $"Nobody covers {task.Address} in this category yet. It is open to any partner."
                : $"{tried} partner(s) were offered it and none took it. It is now open to any partner.";

            await _notifications.PushManyAsync(admins.Select(id => new NotificationRequest(
                id,
                NotificationTypes.NobodyAvailable,
                $"Job #{task.Id} still has no partner",
                body,
                "/admin/tasks?status=pending")), ct);
        }

    }
}
