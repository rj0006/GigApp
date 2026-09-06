using GigApp.Api.Data;
using GigApp.Api.Models;
using GigApp.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Booking
{
    public enum TaskClaimOutcome
    {
        Claimed,
        NotFound,
        NotAllowed,
        Taken,
    }

    public record TaskClaimResult(TaskClaimOutcome Outcome, string? Error, decimal Amount = 0)
    {
        public bool Succeeded => Outcome == TaskClaimOutcome.Claimed;

        public static TaskClaimResult Ok(decimal amount) =>
            new(TaskClaimOutcome.Claimed, null, amount);

        public static TaskClaimResult NotFound() =>
            new(TaskClaimOutcome.NotFound, "Task not found.");

        public static TaskClaimResult NotAllowed(string error) =>
            new(TaskClaimOutcome.NotAllowed, error);

        public static TaskClaimResult Taken() =>
            new(TaskClaimOutcome.Taken, "Another partner has already taken this job.");
    }

    /// <summary>
    /// A partner claiming a fixed-price job outright. Bidding work is never
    /// claimable this way — there the customer chooses, and letting a partner
    /// grab it would make the bids they are comparing worthless.
    /// </summary>
    public interface ITaskClaimService
    {
        Task<TaskClaimResult> ClaimAsync(
            int partnerUserId, int taskId, CancellationToken ct = default);

        Task<TaskClaimResult> AssignAsync(
            int adminUserId, int taskId, int partnerId, string note, CancellationToken ct = default);
    }

    public class TaskClaimService : ITaskClaimService
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<TaskClaimService> _logger;

        public TaskClaimService(
            AppDbContext context,
            INotificationService notifications,
            ILogger<TaskClaimService> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<TaskClaimResult> ClaimAsync(
            int partnerUserId, int taskId, CancellationToken ct = default)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == partnerUserId, ct);

            if (partner is null)
                return TaskClaimResult.NotAllowed("No partner profile is attached to this account.");

            if (!partner.IsVerified)
                return TaskClaimResult.NotAllowed("Your account is pending KYC verification.");

            var task = await _context.GigTasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new
                {
                    t.Id,
                    t.Status,
                    t.BookingMode,
                    t.CategoryId,
                    t.Budget,
                    t.AgreedAmount,
                })
                .FirstOrDefaultAsync(ct);

            if (task is null) return TaskClaimResult.NotFound();

            if (task.BookingMode != TaskBookingMode.Instant)
                return TaskClaimResult.NotAllowed(
                    "This job is open for bids. Place a bid and wait for the customer to accept it.");

            if (task.CategoryId != partner.SkillCategoryId)
                return TaskClaimResult.NotAllowed("This job is outside your skill category.");

            if (task.Status != GigTaskStatus.Pending)
                return TaskClaimResult.Taken();

            // A live offer is first refusal for the partner holding it. Once it
            // expires the job opens to everyone, so nobody is ever stranded by
            // a partner who stopped looking at their phone.
            var held = await _context.TaskOffers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.GigTaskId == taskId
                                       && o.Status == OfferStatus.Pending
                                       && o.ExpiresAt > DateTime.UtcNow, ct);

            if (held is not null && held.PartnerId != partner.Id)
                return TaskClaimResult.NotAllowed(
                    "This job is with another partner right now. If they pass on it, it comes back to the board.");

            var rowsAffected = await _context.GigTasks
                .Where(t => t.Id == taskId
                         && t.Status == GigTaskStatus.Pending
                         && t.BookingMode == TaskBookingMode.Instant)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.PartnerId, partner.Id)
                    .SetProperty(t => t.Status, GigTaskStatus.Accepted), ct);

            if (rowsAffected == 0) return TaskClaimResult.Taken();

            _logger.LogInformation(
                "Instant task {TaskId} claimed by partner {PartnerId}", taskId, partner.Id);

            await TellCustomerAsync(taskId, ct);

            return TaskClaimResult.Ok(task.AgreedAmount ?? task.Budget);
        }

        // Every way a job becomes somebody's runs through this class, so the
        // customer is told from here rather than from each caller.
        private async Task TellCustomerAsync(int taskId, CancellationToken ct)
        {
            var assigned = await _context.GigTasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new
                {
                    t.CustomerId,
                    PartnerName = t.Partner!.User!.Name,
                    PartnerPhone = t.Partner.User.Phone,
                })
                .FirstOrDefaultAsync(ct);

            if (assigned is null) return;

            await _notifications.PushAsync(new NotificationRequest(
                assigned.CustomerId,
                NotificationTypes.JobAssigned,
                "A partner has taken your booking",
                $"{assigned.PartnerName} ({assigned.PartnerPhone}) is on job #{taskId}.",
                "/customer/profile/tasks"), ct);
        }

        public async Task<TaskClaimResult> AssignAsync(
            int adminUserId, int taskId, int partnerId, string note, CancellationToken ct = default)
        {
            note = note?.Trim() ?? string.Empty;

            if (note.Length == 0)
                return TaskClaimResult.NotAllowed(
                    "Write why this partner is being assigned. The note is what the next person reads.");

            var partner = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == partnerId, ct);

            if (partner is null) return TaskClaimResult.NotAllowed("Choose a partner.");

            if (!partner.IsVerified)
                return TaskClaimResult.NotAllowed(
                    $"{partner.User?.Name} has not passed KYC, so they cannot be given work.");

            if (partner.User is { IsActive: false })
                return TaskClaimResult.NotAllowed($"{partner.User.Name}'s account is deactivated.");

            var task = await _context.GigTasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new { t.Id, t.Status, t.CategoryId, t.Budget, t.AgreedAmount })
                .FirstOrDefaultAsync(ct);

            if (task is null) return TaskClaimResult.NotFound();

            if (task.CategoryId != partner.SkillCategoryId)
                return TaskClaimResult.NotAllowed(
                    $"{partner.User?.Name} works in a different category, so they cannot take this job.");

            if (task.Status != GigTaskStatus.Pending)
                return TaskClaimResult.NotAllowed(
                    $"A {task.Status.Replace('_', ' ')} task cannot be assigned.");

            var amount = task.AgreedAmount ?? task.Budget;

            var rowsAffected = await _context.GigTasks
                .Where(t => t.Id == taskId && t.Status == GigTaskStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.PartnerId, partner.Id)
                    .SetProperty(t => t.Status, GigTaskStatus.Accepted)
                    .SetProperty(t => t.AgreedAmount, amount)
                    .SetProperty(t => t.AssignedByUserId, adminUserId)
                    .SetProperty(t => t.AssignedAt, DateTime.UtcNow)
                    .SetProperty(t => t.AssignmentNote, note), ct);

            if (rowsAffected == 0) return TaskClaimResult.Taken();

            await _context.TaskBids
                .Where(b => b.GigTaskId == taskId && BidStatus.Open.Contains(b.Status))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.Status, BidStatus.Rejected)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), ct);

            await TellCustomerAsync(taskId, ct);

            _logger.LogInformation(
                "Task {TaskId} assigned to partner {PartnerId} by user {AdminUserId}",
                taskId, partner.Id, adminUserId);

            return TaskClaimResult.Ok(amount);
        }
    }
}
