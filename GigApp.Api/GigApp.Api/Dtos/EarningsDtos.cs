using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class LedgerEntryDto
    {
        public long Id { get; set; }
        public int PartnerId { get; set; }
        public string EntryType { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Remark { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }

        public int? GigTaskId { get; set; }
        public string? TaskCategory { get; set; }
        public string? TaskAddress { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? TaskCompletedAt { get; set; }

        public string TypeLabel => LedgerEntryTypes.Label(EntryType);
        public string BadgeClass => LedgerEntryTypes.BadgeClass(EntryType);
        public bool IsCredit => Direction == LedgerDirection.Credit;
        public decimal SignedAmount => IsCredit ? Amount : -Amount;

        public static LedgerEntryDto From(LedgerEntry entry) => new()
        {
            Id = entry.Id,
            PartnerId = entry.PartnerId,
            EntryType = entry.EntryType,
            Direction = entry.Direction,
            Amount = entry.Amount,
            BalanceAfter = entry.BalanceAfter,
            Description = entry.Description,
            Reference = entry.Reference,
            Remark = entry.Remark,
            CreatedByName = entry.CreatedByName,
            CreatedAt = entry.CreatedAt,
            GigTaskId = entry.GigTaskId,
            TaskCategory = entry.GigTask?.Category?.Name,
            TaskAddress = entry.GigTask?.Address,
            CustomerName = entry.GigTask?.Customer?.Name,
            TaskCompletedAt = entry.GigTask?.CompletedAt,
        };
    }

    public class EarningsSummaryDto
    {
        public decimal Balance { get; set; }
        public decimal LifetimeEarned { get; set; }
        public decimal LifetimeCommission { get; set; }
        public decimal LifetimePaidOut { get; set; }
        public decimal EarnedThisMonth { get; set; }
        public int JobsPaid { get; set; }
        public decimal CommissionPercent { get; set; }

        public decimal LifetimeNet => LifetimeEarned - LifetimeCommission;
    }

    public class PartnerBalanceDto
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? SkillCategoryName { get; set; }
        public decimal Balance { get; set; }
        public decimal LifetimeEarned { get; set; }
        public decimal LifetimePaidOut { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public static PartnerBalanceDto From(PartnerWallet wallet) => new()
        {
            PartnerId = wallet.PartnerId,
            PartnerName = wallet.Partner?.User?.Name ?? string.Empty,
            Phone = wallet.Partner?.User?.Phone,
            SkillCategoryName = wallet.Partner?.SkillCategory?.Name,
            Balance = wallet.Balance,
            LifetimeEarned = wallet.LifetimeEarned,
            LifetimePaidOut = wallet.LifetimePaidOut,
            UpdatedAt = wallet.UpdatedAt,
        };
    }

    public class RecordPayoutRequest
    {
        [Range(1, 1000000)]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [StringLength(60)]
        [Display(Name = "Bank reference")]
        public string? Reference { get; set; }

        [StringLength(300)]
        [Display(Name = "Remark")]
        public string? Remark { get; set; }
    }

    public class RecordAdjustmentRequest
    {
        [Range(1, 1000000)]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required, StringLength(10)]
        [Display(Name = "Direction")]
        public string Direction { get; set; } = LedgerDirection.Credit;

        [Required, StringLength(300)]
        [Display(Name = "Reason")]
        public string Remark { get; set; } = string.Empty;
    }

    public class ErrorLogDto
    {
        public long Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? InnerMessage { get; set; }
        public string? Module { get; set; }
        public string? RequestPath { get; set; }
        public string? RequestMethod { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public DateTime OccurredAt { get; set; }
        public bool IsResolved { get; set; }
        public string? ResolutionNote { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public string ShortType => ExceptionType.Split('.').LastOrDefault() ?? ExceptionType;

        public static ErrorLogDto From(ErrorLog log) => new()
        {
            Id = log.Id,
            Reference = log.Reference,
            Message = log.Message,
            ExceptionType = log.ExceptionType,
            StackTrace = log.StackTrace,
            InnerMessage = log.InnerMessage,
            Module = log.Module,
            RequestPath = log.RequestPath,
            RequestMethod = log.RequestMethod,
            UserName = log.UserName,
            IpAddress = log.IpAddress,
            OccurredAt = log.OccurredAt,
            IsResolved = log.IsResolved,
            ResolutionNote = log.ResolutionNote,
            ResolvedAt = log.ResolvedAt,
        };
    }
}
