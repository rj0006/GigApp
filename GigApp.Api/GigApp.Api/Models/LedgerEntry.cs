namespace GigApp.Api.Models
{
    public class LedgerEntry
    {
        public long Id { get; set; }

        public int PartnerId { get; set; }
        public Partner? Partner { get; set; }

        public string EntryType { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;

        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }

        public int? GigTaskId { get; set; }
        public GigTask? GigTask { get; set; }

        public string Description { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;

        public string? Reference { get; set; }
        public string? Remark { get; set; }

        public int? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class LedgerEntryTypes
    {
        public const string JobEarning = "job_earning";
        public const string PlatformCommission = "platform_commission";
        public const string Payout = "payout";
        public const string Adjustment = "adjustment";

        public static readonly string[] All =
            { JobEarning, PlatformCommission, Payout, Adjustment };

        public static bool IsValid(string? value) => value is not null && All.Contains(value);

        public static string Label(string value) => value switch
        {
            JobEarning => "Job earning",
            PlatformCommission => "Platform commission",
            Payout => "Payout",
            _ => "Adjustment",
        };

        public static string BadgeClass(string value) => value switch
        {
            JobEarning => "text-bg-success",
            PlatformCommission => "text-bg-secondary",
            Payout => "text-bg-primary",
            _ => "text-bg-warning",
        };
    }

    public static class LedgerDirection
    {
        public const string Credit = "credit";
        public const string Debit = "debit";

        public static readonly string[] All = { Credit, Debit };

        public static bool IsValid(string? value) => value is not null && All.Contains(value);
    }

    public static class PlatformFees
    {
        public const decimal CommissionPercent = 15m;

        public static decimal CommissionOn(decimal amount) =>
            Math.Round(amount * CommissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
