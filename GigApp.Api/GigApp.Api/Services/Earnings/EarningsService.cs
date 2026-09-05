using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Earnings
{
    public interface IEarningsService
    {
        Task<LedgerResult> PostJobEarningAsync(GigTask task, CancellationToken ct = default);

        Task<LedgerResult> PostPayoutAsync(
            int partnerId, RecordPayoutRequest request, int byUserId, string? byName,
            CancellationToken ct = default);

        Task<LedgerResult> PostAdjustmentAsync(
            int partnerId, RecordAdjustmentRequest request, int byUserId, string? byName,
            CancellationToken ct = default);

        Task<EarningsSummaryDto> GetSummaryAsync(int partnerId, CancellationToken ct = default);

        Task<PagedResult<LedgerEntryDto>> GetEntriesAsync(
            int partnerId, PageRequest paging, string? entryType, CancellationToken ct = default);

        Task<IReadOnlyList<PartnerBalanceDto>> GetBalancesAsync(CancellationToken ct = default);
    }

    public class EarningsService : IEarningsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EarningsService> _logger;

        public EarningsService(AppDbContext context, ILogger<EarningsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<LedgerResult> PostJobEarningAsync(
            GigTask task, CancellationToken ct = default)
        {
            if (task.PartnerId is null)
                return LedgerResult.Fail("This task has no partner to pay.");

            if (task.Status != GigTaskStatus.Completed)
                return LedgerResult.Fail("Only a completed task earns.");

            var gross = task.AgreedAmount ?? task.Budget;
            if (gross <= 0) return LedgerResult.Fail("This task settled at zero.");

            var commission = PlatformFees.CommissionOn(gross);

            var earningKey = Key(LedgerEntryTypes.JobEarning, task.Id);
            if (await _context.LedgerEntries.AnyAsync(e => e.IdempotencyKey == earningKey, ct))
                return LedgerResult.AlreadyPosted();

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var wallet = await LoadWalletAsync(task.PartnerId.Value, ct);

            var earning = Append(wallet, new LedgerEntry
            {
                PartnerId = wallet.PartnerId,
                EntryType = LedgerEntryTypes.JobEarning,
                Direction = LedgerDirection.Credit,
                Amount = gross,
                GigTaskId = task.Id,
                Description = $"Task #{task.Id} completed",
                IdempotencyKey = earningKey,
            });

            LedgerEntry? commissionEntry = null;

            if (commission > 0)
            {
                commissionEntry = Append(wallet, new LedgerEntry
                {
                    PartnerId = wallet.PartnerId,
                    EntryType = LedgerEntryTypes.PlatformCommission,
                    Direction = LedgerDirection.Debit,
                    Amount = commission,
                    GigTaskId = task.Id,
                    Description = $"Platform commission at {PlatformFees.CommissionPercent:0.##}%",
                    IdempotencyKey = Key(LedgerEntryTypes.PlatformCommission, task.Id),
                });
            }

            wallet.LifetimeEarned += gross;
            wallet.LifetimeCommission += commission;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Posted earning of {Gross} less {Commission} for partner {PartnerId} on task {TaskId}",
                gross, commission, wallet.PartnerId, task.Id);

            return LedgerResult.Ok(LedgerEntryDto.From(commissionEntry ?? earning));
        }

        public async Task<LedgerResult> PostPayoutAsync(
            int partnerId, RecordPayoutRequest request, int byUserId, string? byName,
            CancellationToken ct = default)
        {
            if (request.Amount <= 0) return LedgerResult.Fail("Enter an amount above zero.");

            var key = string.IsNullOrWhiteSpace(request.Reference)
                ? Key(LedgerEntryTypes.Payout, $"{partnerId}-{Guid.NewGuid():N}")
                : Key(LedgerEntryTypes.Payout, $"{partnerId}-{request.Reference.Trim()}");

            if (await _context.LedgerEntries.AnyAsync(e => e.IdempotencyKey == key, ct))
                return LedgerResult.Fail("A payout with that reference is already recorded.");

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var wallet = await LoadWalletAsync(partnerId, ct);

            if (request.Amount > wallet.Balance)
                return LedgerResult.Fail(
                    $"That is more than the available balance of {wallet.Balance:N2}.");

            var entry = Append(wallet, new LedgerEntry
            {
                PartnerId = partnerId,
                EntryType = LedgerEntryTypes.Payout,
                Direction = LedgerDirection.Debit,
                Amount = request.Amount,
                Description = "Payout to registered bank account",
                IdempotencyKey = key,
                Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
                Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                CreatedByUserId = byUserId,
                CreatedByName = byName,
            });

            wallet.LifetimePaidOut += request.Amount;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return LedgerResult.Ok(LedgerEntryDto.From(entry));
        }

        public async Task<LedgerResult> PostAdjustmentAsync(
            int partnerId, RecordAdjustmentRequest request, int byUserId, string? byName,
            CancellationToken ct = default)
        {
            if (request.Amount <= 0) return LedgerResult.Fail("Enter an amount above zero.");
            if (string.IsNullOrWhiteSpace(request.Remark))
                return LedgerResult.Fail("An adjustment needs a reason.");

            if (!LedgerDirection.IsValid(request.Direction))
                return LedgerResult.Fail("Choose whether this adds to or takes from the balance.");

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var wallet = await LoadWalletAsync(partnerId, ct);

            if (request.Direction == LedgerDirection.Debit && request.Amount > wallet.Balance)
                return LedgerResult.Fail(
                    $"That is more than the available balance of {wallet.Balance:N2}.");

            var entry = Append(wallet, new LedgerEntry
            {
                PartnerId = partnerId,
                EntryType = LedgerEntryTypes.Adjustment,
                Direction = request.Direction,
                Amount = request.Amount,
                Description = "Manual adjustment",
                IdempotencyKey = Key(LedgerEntryTypes.Adjustment, $"{partnerId}-{Guid.NewGuid():N}"),
                Remark = request.Remark.Trim(),
                CreatedByUserId = byUserId,
                CreatedByName = byName,
            });

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return LedgerResult.Ok(LedgerEntryDto.From(entry));
        }

        public async Task<EarningsSummaryDto> GetSummaryAsync(
            int partnerId, CancellationToken ct = default)
        {
            var wallet = await _context.PartnerWallets
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.PartnerId == partnerId, ct);

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var thisMonth = await _context.LedgerEntries
                .AsNoTracking()
                .Where(e => e.PartnerId == partnerId
                         && e.EntryType == LedgerEntryTypes.JobEarning
                         && e.CreatedAt >= monthStart)
                .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

            var jobsPaid = await _context.LedgerEntries
                .AsNoTracking()
                .CountAsync(e => e.PartnerId == partnerId
                              && e.EntryType == LedgerEntryTypes.JobEarning, ct);

            return new EarningsSummaryDto
            {
                Balance = wallet?.Balance ?? 0m,
                LifetimeEarned = wallet?.LifetimeEarned ?? 0m,
                LifetimeCommission = wallet?.LifetimeCommission ?? 0m,
                LifetimePaidOut = wallet?.LifetimePaidOut ?? 0m,
                EarnedThisMonth = thisMonth,
                JobsPaid = jobsPaid,
                CommissionPercent = PlatformFees.CommissionPercent,
            };
        }

        public async Task<PagedResult<LedgerEntryDto>> GetEntriesAsync(
            int partnerId, PageRequest paging, string? entryType, CancellationToken ct = default)
        {
            var query = _context.LedgerEntries
                .AsNoTracking()
                .Include(e => e.GigTask)!.ThenInclude(t => t!.Category)
                .Include(e => e.GigTask)!.ThenInclude(t => t!.Customer)
                .Where(e => e.PartnerId == partnerId);

            if (LedgerEntryTypes.IsValid(entryType))
                query = query.Where(e => e.EntryType == entryType);

            var page = await query
                .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
                .ToPagedResultAsync(paging, ct);

            return page.Map(LedgerEntryDto.From);
        }

        public async Task<IReadOnlyList<PartnerBalanceDto>> GetBalancesAsync(
            CancellationToken ct = default)
        {
            var wallets = await _context.PartnerWallets
                .AsNoTracking()
                .Include(w => w.Partner)!.ThenInclude(p => p!.User)
                .Include(w => w.Partner)!.ThenInclude(p => p!.SkillCategory)
                .OrderByDescending(w => w.Balance)
                .ToListAsync(ct);

            return wallets.Select(PartnerBalanceDto.From).ToList();
        }

        private async Task<PartnerWallet> LoadWalletAsync(int partnerId, CancellationToken ct)
        {
            var wallet = await _context.PartnerWallets
                .FirstOrDefaultAsync(w => w.PartnerId == partnerId, ct);

            if (wallet is null)
            {
                wallet = new PartnerWallet { PartnerId = partnerId, CreatedAt = DateTime.UtcNow };
                _context.PartnerWallets.Add(wallet);
            }

            return wallet;
        }

        private LedgerEntry Append(PartnerWallet wallet, LedgerEntry entry)
        {
            wallet.Balance += entry.Direction == LedgerDirection.Credit ? entry.Amount : -entry.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            entry.BalanceAfter = wallet.Balance;
            entry.CreatedAt = DateTime.UtcNow;

            _context.LedgerEntries.Add(entry);
            return entry;
        }

        private static string Key(string entryType, object id) => $"{entryType}:{id}";
    }

    public class LedgerResult
    {
        public bool Succeeded { get; private init; }
        public bool WasAlreadyPosted { get; private init; }
        public string? Error { get; private init; }
        public LedgerEntryDto? Entry { get; private init; }

        public static LedgerResult Ok(LedgerEntryDto entry) =>
            new() { Succeeded = true, Entry = entry };

        public static LedgerResult AlreadyPosted() =>
            new() { Succeeded = true, WasAlreadyPosted = true };

        public static LedgerResult Fail(string error) =>
            new() { Succeeded = false, Error = error };
    }
}
