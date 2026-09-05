using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Earnings
{
    public interface IPlanService
    {
        Task<IReadOnlyList<CommissionPlanDto>> GetPlansAsync(
            bool activeOnly, CancellationToken ct = default);

        Task<CommissionPlanDto?> GetPlanAsync(int id, CancellationToken ct = default);

        Task<PlanResult> SavePlanAsync(
            int? id, SaveCommissionPlanRequest request, CancellationToken ct = default);

        Task<PartnerPlanDto?> GetPartnerPlanAsync(int partnerId, CancellationToken ct = default);

        Task<IReadOnlyList<PartnerPlanDto>> GetPartnerPlanHistoryAsync(
            int partnerId, CancellationToken ct = default);

        Task<PartnerPlanSubscription> ResolveActivePlanAsync(
            int partnerId, CancellationToken ct = default);

        Task<PlanAssignment> AssignPlanAsync(
            int partnerId, AssignPlanRequest request, int byUserId, string? byName,
            CancellationToken ct = default);
    }

    public class PlanService : IPlanService
    {
        private readonly AppDbContext _context;

        public PlanService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<CommissionPlanDto>> GetPlansAsync(
            bool activeOnly, CancellationToken ct = default)
        {
            var query = _context.CommissionPlans.AsNoTracking();
            if (activeOnly) query = query.Where(p => p.IsActive);

            var plans = await query
                .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                .ToListAsync(ct);

            var counts = await _context.PartnerPlanSubscriptions
                .AsNoTracking()
                .Where(s => s.IsActive)
                .GroupBy(s => s.CommissionPlanId)
                .Select(g => new { PlanId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Count, ct);

            return plans
                .Select(p => CommissionPlanDto.From(p, counts.GetValueOrDefault(p.Id)))
                .ToList();
        }

        public async Task<CommissionPlanDto?> GetPlanAsync(int id, CancellationToken ct = default)
        {
            var plan = await _context.CommissionPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            return plan is null ? null : CommissionPlanDto.From(plan, 0);
        }

        public async Task<PlanResult> SavePlanAsync(
            int? id, SaveCommissionPlanRequest request, CancellationToken ct = default)
        {
            if (!PlanBillingPeriod.IsValid(request.BillingPeriod))
                return PlanResult.Fail("Choose how often the fee is charged.");

            if (request.SubscriptionFee > 0 && request.BillingPeriod == PlanBillingPeriod.None)
                return PlanResult.Fail("A plan with a fee needs a billing period.");

            if (request.SubscriptionFee == 0 && request.BillingPeriod != PlanBillingPeriod.None)
                return PlanResult.Fail("A plan with no fee cannot have a billing period.");

            var code = request.Code.Trim().ToUpperInvariant();

            if (await _context.CommissionPlans.AnyAsync(
                    p => p.Code == code && (id == null || p.Id != id), ct))
            {
                return PlanResult.Fail($"'{code}' is already used by another plan.");
            }

            CommissionPlan plan;

            if (id is null)
            {
                plan = new CommissionPlan { CreatedAt = DateTime.UtcNow };
                _context.CommissionPlans.Add(plan);
            }
            else
            {
                var existing = await _context.CommissionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
                if (existing is null) return PlanResult.Fail("That plan no longer exists.");

                plan = existing;
                plan.UpdatedAt = DateTime.UtcNow;
            }

            plan.Name = request.Name.Trim();
            plan.Code = code;
            plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            plan.CommissionPercent = request.CommissionPercent;
            plan.SubscriptionFee = request.SubscriptionFee;
            plan.BillingPeriod = request.BillingPeriod;
            plan.IsActive = request.IsActive;
            plan.DisplayOrder = request.DisplayOrder;

            if (request.IsDefault)
            {
                var others = await _context.CommissionPlans
                    .Where(p => p.IsDefault && (id == null || p.Id != id))
                    .ToListAsync(ct);

                foreach (var other in others) other.IsDefault = false;
            }

            plan.IsDefault = request.IsDefault;

            await _context.SaveChangesAsync(ct);

            return PlanResult.Ok(CommissionPlanDto.From(plan, 0));
        }

        public async Task<PartnerPlanDto?> GetPartnerPlanAsync(
            int partnerId, CancellationToken ct = default)
        {
            var current = await _context.PartnerPlanSubscriptions
                .AsNoTracking()
                .Include(s => s.CommissionPlan)
                .Where(s => s.PartnerId == partnerId && s.IsActive)
                .OrderByDescending(s => s.StartsOn)
                .FirstOrDefaultAsync(ct);

            return current is null ? null : PartnerPlanDto.From(current);
        }

        public async Task<IReadOnlyList<PartnerPlanDto>> GetPartnerPlanHistoryAsync(
            int partnerId, CancellationToken ct = default)
        {
            var rows = await _context.PartnerPlanSubscriptions
                .AsNoTracking()
                .Include(s => s.CommissionPlan)
                .Where(s => s.PartnerId == partnerId)
                .OrderByDescending(s => s.StartsOn).ThenByDescending(s => s.Id)
                .ToListAsync(ct);

            return rows.Select(PartnerPlanDto.From).ToList();
        }

        public async Task<PlanAssignment> AssignPlanAsync(
            int partnerId, AssignPlanRequest request, int byUserId, string? byName,
            CancellationToken ct = default)
        {
            var plan = await _context.CommissionPlans
                .FirstOrDefaultAsync(p => p.Id == request.CommissionPlanId && p.IsActive, ct);

            if (plan is null) return PlanAssignment.Fail("Choose an active plan.");

            var current = await _context.PartnerPlanSubscriptions
                .Where(s => s.PartnerId == partnerId && s.IsActive)
                .ToListAsync(ct);

            if (current.Any(s => s.CommissionPlanId == plan.Id && !s.HasExpired))
                return PlanAssignment.Fail("The partner is already on that plan.");

            foreach (var previous in current)
            {
                previous.IsActive = false;
                previous.EndsOn = DateTime.UtcNow;
            }

            var months = PlanBillingPeriod.Months(plan.BillingPeriod);

            var subscription = new PartnerPlanSubscription
            {
                PartnerId = partnerId,
                CommissionPlanId = plan.Id,
                CommissionPercent = plan.CommissionPercent,
                FeeCharged = request.ChargeFee ? plan.SubscriptionFee : 0m,
                StartsOn = DateTime.UtcNow,
                EndsOn = months > 0 ? DateTime.UtcNow.AddMonths(months) : null,
                IsActive = true,
                Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                AssignedByUserId = byUserId,
                AssignedByName = byName,
            };

            _context.PartnerPlanSubscriptions.Add(subscription);
            await _context.SaveChangesAsync(ct);

            return PlanAssignment.Ok(
                subscription,
                chargeFee: request.ChargeFee && plan.SubscriptionFee > 0,
                planId: plan.Id);
        }

        public async Task<PartnerPlanSubscription> ResolveActivePlanAsync(
            int partnerId, CancellationToken ct = default)
        {
            var current = await _context.PartnerPlanSubscriptions
                .Include(s => s.CommissionPlan)
                .Where(s => s.PartnerId == partnerId && s.IsActive)
                .OrderByDescending(s => s.StartsOn)
                .FirstOrDefaultAsync(ct);

            // A subscription that has run out stops carrying its rate; the
            // partner falls back to the default plan rather than keeping a
            // discount they are no longer paying for.
            if (current is not null && !current.HasExpired) return current;

            if (current is not null)
            {
                current.IsActive = false;
                current.EndsOn ??= DateTime.UtcNow;
            }

            var fallback = await _context.CommissionPlans
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.IsDefault).ThenBy(p => p.DisplayOrder)
                .FirstOrDefaultAsync(ct);

            if (fallback is null)
                throw new InvalidOperationException("No active commission plan exists to fall back to.");

            var subscription = new PartnerPlanSubscription
            {
                PartnerId = partnerId,
                CommissionPlanId = fallback.Id,
                CommissionPlan = fallback,
                CommissionPercent = fallback.CommissionPercent,
                FeeCharged = 0m,
                StartsOn = DateTime.UtcNow,
                EndsOn = null,
                IsActive = true,
                Remark = "Assigned automatically as the default plan",
            };

            _context.PartnerPlanSubscriptions.Add(subscription);
            await _context.SaveChangesAsync(ct);

            return subscription;
        }
    }

    public class PlanAssignment
    {
        public bool Succeeded { get; private init; }
        public string? Error { get; private init; }
        public PartnerPlanSubscription? Subscription { get; private init; }
        public bool ChargeFee { get; private init; }
        public int PlanId { get; private init; }

        public static PlanAssignment Ok(
            PartnerPlanSubscription subscription, bool chargeFee, int planId) => new()
            {
                Succeeded = true,
                Subscription = subscription,
                ChargeFee = chargeFee,
                PlanId = planId,
            };

        public static PlanAssignment Fail(string error) =>
            new() { Succeeded = false, Error = error };
    }

    public class PlanResult
    {
        public bool Succeeded { get; private init; }
        public string? Error { get; private init; }
        public CommissionPlanDto? Plan { get; private init; }

        public static PlanResult Ok(CommissionPlanDto plan) => new() { Succeeded = true, Plan = plan };
        public static PlanResult Fail(string error) => new() { Succeeded = false, Error = error };
    }
}
