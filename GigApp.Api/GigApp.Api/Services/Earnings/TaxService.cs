using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Earnings
{
    public interface ITaxService
    {
        Task<IReadOnlyList<TaxRuleDto>> GetRulesAsync(
            string? countryCode, bool activeOnly, CancellationToken ct = default);

        Task<TaxRuleDto?> GetRuleAsync(int id, CancellationToken ct = default);

        Task<TaxResult> SaveRuleAsync(
            int? id, SaveTaxRuleRequest request, CancellationToken ct = default);

        Task<IReadOnlyList<TaxRule>> RulesInForceAsync(
            string countryCode, DateTime moment, CancellationToken ct = default);

        IReadOnlyList<TaxCharge> Compute(
            IReadOnlyList<TaxRule> rules, decimal grossEarning, decimal commission, decimal subscriptionFee);
    }

    public record TaxCharge(TaxRule Rule, decimal BaseAmount, decimal Amount);

    public class TaxService : ITaxService
    {
        private readonly AppDbContext _context;

        public TaxService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<TaxRuleDto>> GetRulesAsync(
            string? countryCode, bool activeOnly, CancellationToken ct = default)
        {
            var query = _context.TaxRules.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(countryCode))
                query = query.Where(t => t.CountryCode == countryCode);

            if (activeOnly) query = query.Where(t => t.IsActive);

            var rules = await query
                .OrderBy(t => t.CountryCode).ThenBy(t => t.SortOrder).ThenBy(t => t.Name)
                .ToListAsync(ct);

            return rules.Select(TaxRuleDto.From).ToList();
        }

        public async Task<TaxRuleDto?> GetRuleAsync(int id, CancellationToken ct = default)
        {
            var rule = await _context.TaxRules.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            return rule is null ? null : TaxRuleDto.From(rule);
        }

        public async Task<TaxResult> SaveRuleAsync(
            int? id, SaveTaxRuleRequest request, CancellationToken ct = default)
        {
            if (!TaxBase.IsValid(request.AppliesTo))
                return TaxResult.Fail("Choose what this tax is calculated on.");

            if (request.EffectiveTo is not null && request.EffectiveTo <= request.EffectiveFrom)
                return TaxResult.Fail("The end date has to be after the start date.");

            var code = request.Code.Trim().ToUpperInvariant();
            var country = request.CountryCode.Trim().ToUpperInvariant();

            if (await _context.TaxRules.AnyAsync(
                    t => t.Code == code && t.CountryCode == country && (id == null || t.Id != id), ct))
            {
                return TaxResult.Fail($"'{code}' already exists for {country}.");
            }

            TaxRule rule;

            if (id is null)
            {
                rule = new TaxRule { CreatedAt = DateTime.UtcNow };
                _context.TaxRules.Add(rule);
            }
            else
            {
                var existing = await _context.TaxRules.FirstOrDefaultAsync(t => t.Id == id, ct);
                if (existing is null) return TaxResult.Fail("That rule no longer exists.");

                rule = existing;
                rule.UpdatedAt = DateTime.UtcNow;
            }

            rule.Name = request.Name.Trim();
            rule.Code = code;
            rule.CountryCode = country;
            rule.Percent = request.Percent;
            rule.AppliesTo = request.AppliesTo;
            rule.ThresholdAmount = request.ThresholdAmount;
            rule.EffectiveFrom = request.EffectiveFrom.ToUtc();
            rule.EffectiveTo = request.EffectiveTo?.ToUtc();
            rule.IsActive = request.IsActive;
            rule.SortOrder = request.SortOrder;
            rule.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            await _context.SaveChangesAsync(ct);

            return TaxResult.Ok(TaxRuleDto.From(rule));
        }

        public async Task<IReadOnlyList<TaxRule>> RulesInForceAsync(
            string countryCode, DateTime moment, CancellationToken ct = default)
        {
            var candidates = await _context.TaxRules
                .AsNoTracking()
                .Where(t => t.CountryCode == countryCode && t.IsActive)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .ToListAsync(ct);

            return candidates.Where(t => t.AppliesOn(moment)).ToList();
        }

        public IReadOnlyList<TaxCharge> Compute(
            IReadOnlyList<TaxRule> rules,
            decimal grossEarning,
            decimal commission,
            decimal subscriptionFee)
        {
            var charges = new List<TaxCharge>();

            foreach (var rule in rules)
            {
                var baseAmount = rule.AppliesTo switch
                {
                    TaxBase.GrossEarning => grossEarning,
                    TaxBase.SubscriptionFee => subscriptionFee,
                    _ => commission,
                };

                if (baseAmount <= 0 || baseAmount < rule.ThresholdAmount) continue;

                var amount = Money.Percent(baseAmount, rule.Percent);
                if (amount <= 0) continue;

                charges.Add(new TaxCharge(rule, baseAmount, amount));
            }

            return charges;
        }
    }

    public class TaxResult
    {
        public bool Succeeded { get; private init; }
        public string? Error { get; private init; }
        public TaxRuleDto? Rule { get; private init; }

        public static TaxResult Ok(TaxRuleDto rule) => new() { Succeeded = true, Rule = rule };
        public static TaxResult Fail(string error) => new() { Succeeded = false, Error = error };
    }
}
