using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Bidding
{
    /// <summary>
    /// Every bidding rule lives here. The API controller and the Razor portals
    /// both call it, so web and mobile enforce exactly the same thing.
    /// </summary>
    public interface IBidService
    {
        Task<BidResult> PlaceAsync(int partnerUserId, int taskId, PlaceBidRequest request, CancellationToken ct = default);
        Task<BidResult> WithdrawAsync(int partnerUserId, int bidId, CancellationToken ct = default);

        /// <summary>Partner accepts the customer's counter offer — this assigns the task.</summary>
        Task<BidResult> AcceptCounterAsync(int partnerUserId, int bidId, CancellationToken ct = default);

        Task<BidResult> CounterAsync(int customerUserId, int bidId, CounterBidRequest request, CancellationToken ct = default);
        Task<BidResult> AcceptAsync(int customerUserId, int bidId, CancellationToken ct = default);
        Task<BidResult> RejectAsync(int customerUserId, int bidId, CancellationToken ct = default);

        Task<IReadOnlyList<BidDto>> ForTaskAsync(int customerUserId, int taskId, CancellationToken ct = default);
        Task<IReadOnlyList<BidDto>> ForPartnerAsync(int partnerUserId, CancellationToken ct = default);
    }

    public class BidService : IBidService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BidService> _logger;

        public BidService(AppDbContext context, ILogger<BidService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ------------------------------------------------------- partner side

        public async Task<BidResult> PlaceAsync(
            int partnerUserId, int taskId, PlaceBidRequest request, CancellationToken ct = default)
        {
            var partner = await LoadPartnerAsync(partnerUserId, ct);
            if (partner is null) return BidResult.Fail("No partner profile is attached to this account.");
            if (!partner.IsVerified) return BidResult.Fail("Your account is pending KYC verification.");

            var task = await _context.GigTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null) return BidResult.Fail("Task not found.");

            if (task.Status != GigTaskStatus.Pending)
                return BidResult.Fail("This task is no longer open for bids.");

            // The whole point of one shared category master is that a plumber
            // cannot bid on a painting job.
            if (task.CategoryId != partner.SkillCategoryId)
                return BidResult.Fail("This task is outside your skill category.");

            var existing = await _context.TaskBids
                .FirstOrDefaultAsync(b => b.GigTaskId == taskId && b.PartnerId == partner.Id, ct);

            if (existing is null)
            {
                existing = new TaskBid
                {
                    GigTaskId = taskId,
                    PartnerId = partner.Id,
                    CreatedAt = DateTime.UtcNow,
                };
                _context.TaskBids.Add(existing);
            }
            else if (existing.Status == BidStatus.Accepted)
            {
                return BidResult.Fail("This bid has already been accepted.");
            }
            else
            {
                existing.UpdatedAt = DateTime.UtcNow;
            }

            // Re-bidding after a counter clears the counter — the ball moves back
            // to the customer.
            existing.Amount = request.Amount;
            existing.Note = Trim(request.Note);
            existing.CounterAmount = null;
            existing.CounterNote = null;
            existing.Status = BidStatus.Pending;

            await _context.SaveChangesAsync(ct);

            return BidResult.Ok(await ReloadAsync(existing.Id, ct));
        }

        public async Task<BidResult> WithdrawAsync(int partnerUserId, int bidId, CancellationToken ct = default)
        {
            var (bid, error) = await LoadOwnBidAsync(partnerUserId, bidId, ct);
            if (error is not null) return BidResult.Fail(error);

            if (!BidStatus.IsOpen(bid!.Status))
                return BidResult.Fail("This bid can no longer be withdrawn.");

            bid.Status = BidStatus.Withdrawn;
            bid.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return BidResult.Ok(await ReloadAsync(bid.Id, ct));
        }

        public async Task<BidResult> AcceptCounterAsync(int partnerUserId, int bidId, CancellationToken ct = default)
        {
            var (bid, error) = await LoadOwnBidAsync(partnerUserId, bidId, ct);
            if (error is not null) return BidResult.Fail(error);

            if (bid!.Status != BidStatus.Countered)
                return BidResult.Fail("There is no counter offer to accept on this bid.");

            return await AwardAsync(bid, bid.CounterAmount ?? bid.Amount, ct);
        }

        // ------------------------------------------------------ customer side

        public async Task<BidResult> CounterAsync(
            int customerUserId, int bidId, CounterBidRequest request, CancellationToken ct = default)
        {
            var (bid, error) = await LoadBidOnOwnTaskAsync(customerUserId, bidId, ct);
            if (error is not null) return BidResult.Fail(error);

            if (bid!.Status != BidStatus.Pending)
                return BidResult.Fail("You can only counter a bid that is waiting on you.");

            bid.CounterAmount = request.CounterAmount;
            bid.CounterNote = Trim(request.CounterNote);
            bid.Status = BidStatus.Countered;
            bid.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            return BidResult.Ok(await ReloadAsync(bid.Id, ct));
        }

        public async Task<BidResult> AcceptAsync(int customerUserId, int bidId, CancellationToken ct = default)
        {
            var (bid, error) = await LoadBidOnOwnTaskAsync(customerUserId, bidId, ct);
            if (error is not null) return BidResult.Fail(error);

            if (!BidStatus.IsOpen(bid!.Status))
                return BidResult.Fail("This bid is no longer open.");

            // Accepting a countered bid means accepting at the partner's original
            // asking price — the customer is dropping their own counter.
            return await AwardAsync(bid, bid.Amount, ct);
        }

        public async Task<BidResult> RejectAsync(int customerUserId, int bidId, CancellationToken ct = default)
        {
            var (bid, error) = await LoadBidOnOwnTaskAsync(customerUserId, bidId, ct);
            if (error is not null) return BidResult.Fail(error);

            if (!BidStatus.IsOpen(bid!.Status))
                return BidResult.Fail("This bid is no longer open.");

            bid.Status = BidStatus.Rejected;
            bid.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return BidResult.Ok(await ReloadAsync(bid.Id, ct));
        }

        // ------------------------------------------------------------- reads

        public async Task<IReadOnlyList<BidDto>> ForTaskAsync(
            int customerUserId, int taskId, CancellationToken ct = default)
        {
            var ownsTask = await _context.GigTasks
                .AnyAsync(t => t.Id == taskId && t.CustomerId == customerUserId, ct);

            if (!ownsTask) return Array.Empty<BidDto>();

            var bids = await DetailedBids
                .Where(b => b.GigTaskId == taskId && b.Status != BidStatus.Withdrawn)
                .OrderBy(b => b.Status == BidStatus.Accepted ? 0 : 1)
                .ThenBy(b => b.Amount)
                .ToListAsync(ct);

            return bids.Select(BidDto.From).ToList();
        }

        public async Task<IReadOnlyList<BidDto>> ForPartnerAsync(
            int partnerUserId, CancellationToken ct = default)
        {
            var partner = await LoadPartnerAsync(partnerUserId, ct);
            if (partner is null) return Array.Empty<BidDto>();

            var bids = await DetailedBids
                .Where(b => b.PartnerId == partner.Id)
                // Counters first — those are the ones needing an answer.
                .OrderBy(b => b.Status == BidStatus.Countered ? 0 : 1)
                .ThenByDescending(b => b.UpdatedAt ?? b.CreatedAt)
                .ToListAsync(ct);

            return bids.Select(BidDto.From).ToList();
        }

        // ----------------------------------------------------------- helpers

        /// <summary>
        /// Assigns the task to this bid's partner and closes every other bid.
        /// The conditional update is the lock: two customers clicking accept on
        /// different bids at the same moment cannot both win.
        /// </summary>
        private async Task<BidResult> AwardAsync(TaskBid bid, decimal agreedAmount, CancellationToken ct)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var rows = await _context.GigTasks
                .Where(t => t.Id == bid.GigTaskId
                         && t.Status == GigTaskStatus.Pending
                         && t.PartnerId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.PartnerId, bid.PartnerId)
                    .SetProperty(t => t.Status, GigTaskStatus.Accepted)
                    .SetProperty(t => t.AgreedAmount, agreedAmount)
                    .SetProperty(t => t.AcceptedBidId, bid.Id), ct);

            if (rows == 0)
            {
                await transaction.RollbackAsync(ct);
                return BidResult.Fail("This task has already been assigned to someone else.");
            }

            // Everything else on this task is off the table.
            await _context.TaskBids
                .Where(b => b.GigTaskId == bid.GigTaskId
                         && b.Id != bid.Id
                         && (b.Status == BidStatus.Pending || b.Status == BidStatus.Countered))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, BidStatus.Rejected)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), ct);

            await _context.TaskBids
                .Where(b => b.Id == bid.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, BidStatus.Accepted)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Task {TaskId} awarded to partner {PartnerId} at {Amount}",
                bid.GigTaskId, bid.PartnerId, agreedAmount);

            return BidResult.Ok(await ReloadAsync(bid.Id, ct));
        }

        private IQueryable<TaskBid> DetailedBids =>
            _context.TaskBids
                .AsNoTracking()
                .Include(b => b.Partner)!.ThenInclude(p => p!.User)
                .Include(b => b.Partner)!.ThenInclude(p => p!.SkillCategory)
                .Include(b => b.GigTask)!.ThenInclude(t => t!.Category);

        private async Task<BidDto> ReloadAsync(int bidId, CancellationToken ct) =>
            BidDto.From(await DetailedBids.FirstAsync(b => b.Id == bidId, ct));

        private Task<Partner?> LoadPartnerAsync(int userId, CancellationToken ct) =>
            _context.Partners.FirstOrDefaultAsync(p => p.UserId == userId, ct);

        private async Task<(TaskBid? Bid, string? Error)> LoadOwnBidAsync(
            int partnerUserId, int bidId, CancellationToken ct)
        {
            var partner = await LoadPartnerAsync(partnerUserId, ct);
            if (partner is null) return (null, "No partner profile is attached to this account.");

            var bid = await _context.TaskBids
                .FirstOrDefaultAsync(b => b.Id == bidId && b.PartnerId == partner.Id, ct);

            return bid is null ? (null, "Bid not found.") : (bid, null);
        }

        private async Task<(TaskBid? Bid, string? Error)> LoadBidOnOwnTaskAsync(
            int customerUserId, int bidId, CancellationToken ct)
        {
            var bid = await _context.TaskBids
                .Include(b => b.GigTask)
                .FirstOrDefaultAsync(b => b.Id == bidId, ct);

            if (bid is null) return (null, "Bid not found.");

            // A customer may only act on bids placed against their own task.
            if (bid.GigTask?.CustomerId != customerUserId) return (null, "Bid not found.");

            if (bid.GigTask.Status != GigTaskStatus.Pending)
                return (null, "This task is no longer open for bids.");

            return (bid, null);
        }

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
