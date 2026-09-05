using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class CommissionPlanDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal CommissionPercent { get; set; }
        public decimal SubscriptionFee { get; set; }
        public string BillingPeriod { get; set; } = PlanBillingPeriod.None;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public int PartnersOnPlan { get; set; }

        public bool IsZeroCommission => CommissionPercent == 0m;
        public string BillingLabel => PlanBillingPeriod.Label(BillingPeriod);

        public string Headline => IsZeroCommission
            ? "Keep every rupee you earn"
            : $"{CommissionPercent:0.##}% commission per job";

        public static CommissionPlanDto From(CommissionPlan plan, int partnersOnPlan) => new()
        {
            Id = plan.Id,
            Name = plan.Name,
            Code = plan.Code,
            Description = plan.Description,
            CommissionPercent = plan.CommissionPercent,
            SubscriptionFee = plan.SubscriptionFee,
            BillingPeriod = plan.BillingPeriod,
            IsDefault = plan.IsDefault,
            IsActive = plan.IsActive,
            DisplayOrder = plan.DisplayOrder,
            PartnersOnPlan = partnersOnPlan,
        };
    }

    public class PartnerPlanDto
    {
        public int Id { get; set; }
        public int PartnerId { get; set; }
        public int CommissionPlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public decimal CommissionPercent { get; set; }
        public decimal FeeCharged { get; set; }
        public DateTime StartsOn { get; set; }
        public DateTime? EndsOn { get; set; }
        public bool IsActive { get; set; }
        public string? Remark { get; set; }
        public string? AssignedByName { get; set; }

        public bool IsZeroCommission => CommissionPercent == 0m;

        public static PartnerPlanDto From(PartnerPlanSubscription subscription) => new()
        {
            Id = subscription.Id,
            PartnerId = subscription.PartnerId,
            CommissionPlanId = subscription.CommissionPlanId,
            PlanName = subscription.CommissionPlan?.Name ?? string.Empty,
            PlanCode = subscription.CommissionPlan?.Code ?? string.Empty,
            CommissionPercent = subscription.CommissionPercent,
            FeeCharged = subscription.FeeCharged,
            StartsOn = subscription.StartsOn,
            EndsOn = subscription.EndsOn,
            IsActive = subscription.IsActive,
            Remark = subscription.Remark,
            AssignedByName = subscription.AssignedByName,
        };
    }

    public class SaveCommissionPlanRequest
    {
        [Required, StringLength(80, MinimumLength = 2)]
        [Display(Name = "Plan name")]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(30, MinimumLength = 2)]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [StringLength(400)]
        [Display(Name = "What the partner gets")]
        public string? Description { get; set; }

        [Range(0, 100)]
        [Display(Name = "Commission per job (%)")]
        public decimal CommissionPercent { get; set; }

        [Range(0, 1000000)]
        [Display(Name = "Plan fee")]
        public decimal SubscriptionFee { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Charged")]
        public string BillingPeriod { get; set; } = PlanBillingPeriod.None;

        [Display(Name = "Default plan for new partners")]
        public bool IsDefault { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Range(0, 999)]
        [Display(Name = "Sort order")]
        public int DisplayOrder { get; set; }

        public static SaveCommissionPlanRequest From(CommissionPlanDto plan) => new()
        {
            Name = plan.Name,
            Code = plan.Code,
            Description = plan.Description,
            CommissionPercent = plan.CommissionPercent,
            SubscriptionFee = plan.SubscriptionFee,
            BillingPeriod = plan.BillingPeriod,
            IsDefault = plan.IsDefault,
            IsActive = plan.IsActive,
            DisplayOrder = plan.DisplayOrder,
        };
    }

    public class AssignPlanRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a plan.")]
        [Display(Name = "Plan")]
        public int CommissionPlanId { get; set; }

        [Display(Name = "Charge the plan fee now")]
        public bool ChargeFee { get; set; } = true;

        [StringLength(300)]
        [Display(Name = "Remark")]
        public string? Remark { get; set; }
    }

    public class TaxRuleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string CountryCode { get; set; } = TaxCountries.India;
        public decimal Percent { get; set; }
        public string AppliesTo { get; set; } = TaxBase.Commission;
        public decimal ThresholdAmount { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? Note { get; set; }

        public string AppliesToLabel => TaxBase.Label(AppliesTo);

        public bool IsInForce =>
            IsActive && EffectiveFrom <= DateTime.UtcNow
            && (EffectiveTo is null || EffectiveTo > DateTime.UtcNow);

        public static TaxRuleDto From(TaxRule rule) => new()
        {
            Id = rule.Id,
            Name = rule.Name,
            Code = rule.Code,
            CountryCode = rule.CountryCode,
            Percent = rule.Percent,
            AppliesTo = rule.AppliesTo,
            ThresholdAmount = rule.ThresholdAmount,
            EffectiveFrom = rule.EffectiveFrom,
            EffectiveTo = rule.EffectiveTo,
            IsActive = rule.IsActive,
            SortOrder = rule.SortOrder,
            Note = rule.Note,
        };
    }

    public class SaveTaxRuleRequest
    {
        [Required, StringLength(80, MinimumLength = 2)]
        [Display(Name = "Tax name")]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(30, MinimumLength = 2)]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(2, MinimumLength = 2)]
        [Display(Name = "Country")]
        public string CountryCode { get; set; } = TaxCountries.India;

        [Range(0, 100)]
        [Display(Name = "Rate (%)")]
        public decimal Percent { get; set; }

        [Required, StringLength(30)]
        [Display(Name = "Calculated on")]
        public string AppliesTo { get; set; } = TaxBase.Commission;

        [Range(0, 10000000)]
        [Display(Name = "Apply only above")]
        public decimal ThresholdAmount { get; set; }

        [Required]
        [Display(Name = "In force from")]
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;

        [Display(Name = "In force until")]
        public DateTime? EffectiveTo { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Range(0, 999)]
        [Display(Name = "Sort order")]
        public int SortOrder { get; set; }

        [StringLength(400)]
        [Display(Name = "Note")]
        public string? Note { get; set; }

        public static SaveTaxRuleRequest From(TaxRuleDto rule) => new()
        {
            Name = rule.Name,
            Code = rule.Code,
            CountryCode = rule.CountryCode,
            Percent = rule.Percent,
            AppliesTo = rule.AppliesTo,
            ThresholdAmount = rule.ThresholdAmount,
            EffectiveFrom = rule.EffectiveFrom,
            EffectiveTo = rule.EffectiveTo,
            IsActive = rule.IsActive,
            SortOrder = rule.SortOrder,
            Note = rule.Note,
        };
    }
}
