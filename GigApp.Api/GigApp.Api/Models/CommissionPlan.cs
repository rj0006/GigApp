namespace GigApp.Api.Models
{
    public class CommissionPlan
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }

        public decimal CommissionPercent { get; set; }

        public decimal SubscriptionFee { get; set; }
        public string BillingPeriod { get; set; } = PlanBillingPeriod.None;

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PartnerPlanSubscription> Subscriptions { get; set; } =
            new List<PartnerPlanSubscription>();

        public bool IsZeroCommission => CommissionPercent == 0m;
        public bool IsSubscription => SubscriptionFee > 0m;
    }

    public static class PlanBillingPeriod
    {
        public const string None = "none";
        public const string Monthly = "monthly";
        public const string Quarterly = "quarterly";
        public const string Yearly = "yearly";

        public static readonly string[] All = { None, Monthly, Quarterly, Yearly };

        public static bool IsValid(string? value) => value is not null && All.Contains(value);

        public static string Label(string value) => value switch
        {
            Monthly => "Every month",
            Quarterly => "Every three months",
            Yearly => "Every year",
            _ => "No recurring fee",
        };

        public static int Months(string value) => value switch
        {
            Monthly => 1,
            Quarterly => 3,
            Yearly => 12,
            _ => 0,
        };
    }

    public class PartnerPlanSubscription
    {
        public int Id { get; set; }

        public int PartnerId { get; set; }
        public Partner? Partner { get; set; }

        public int CommissionPlanId { get; set; }
        public CommissionPlan? CommissionPlan { get; set; }

        public decimal CommissionPercent { get; set; }
        public decimal FeeCharged { get; set; }

        public DateTime StartsOn { get; set; } = DateTime.UtcNow;
        public DateTime? EndsOn { get; set; }
        public bool IsActive { get; set; } = true;

        public string? Remark { get; set; }
        public int? AssignedByUserId { get; set; }
        public string? AssignedByName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool HasExpired => EndsOn is not null && EndsOn <= DateTime.UtcNow;
    }
}
