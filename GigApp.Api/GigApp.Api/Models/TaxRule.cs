namespace GigApp.Api.Models
{
    public class TaxRule
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string CountryCode { get; set; } = "IN";

        public decimal Percent { get; set; }
        public string AppliesTo { get; set; } = TaxBase.Commission;

        public decimal ThresholdAmount { get; set; }

        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
        public DateTime? EffectiveTo { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool AppliesOn(DateTime moment) =>
            IsActive && EffectiveFrom <= moment && (EffectiveTo is null || EffectiveTo > moment);
    }

    public static class TaxBase
    {
        public const string Commission = "commission";
        public const string GrossEarning = "gross_earning";
        public const string SubscriptionFee = "subscription_fee";

        public static readonly string[] All = { Commission, GrossEarning, SubscriptionFee };

        public static bool IsValid(string? value) => value is not null && All.Contains(value);

        public static string Label(string value) => value switch
        {
            GrossEarning => "The full job amount",
            SubscriptionFee => "The plan fee",
            _ => "The platform commission",
        };
    }

    public static class TaxCountries
    {
        public const string India = "IN";

        public static readonly string[] All = { India, "AE", "SA", "GB", "US", "SG", "AU" };
    }
}
