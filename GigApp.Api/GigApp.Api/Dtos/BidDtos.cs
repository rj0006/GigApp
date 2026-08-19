using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class PlaceBidRequest
    {
        [Range(1, 10_000_000, ErrorMessage = "Enter an amount greater than zero.")]
        [Display(Name = "Your amount (₹)")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        [Display(Name = "Message to the customer")]
        public string? Note { get; set; }
    }

    public class CounterBidRequest
    {
        [Range(1, 10_000_000, ErrorMessage = "Enter an amount greater than zero.")]
        [Display(Name = "Your counter offer (₹)")]
        public decimal CounterAmount { get; set; }

        [StringLength(500)]
        [Display(Name = "Message to the partner")]
        public string? CounterNote { get; set; }
    }

    public class BidDto
    {
        public int Id { get; set; }
        public int GigTaskId { get; set; }
        public int PartnerId { get; set; }

        public decimal Amount { get; set; }
        public string? Note { get; set; }
        public decimal? CounterAmount { get; set; }
        public string? CounterNote { get; set; }

        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>The figure on the table right now.</summary>
        public decimal CurrentAmount => CounterAmount ?? Amount;

        public bool IsOpen => BidStatus.IsOpen(Status);

        /// <summary>True while the partner still owes an answer to a counter.</summary>
        public bool AwaitingPartner => Status == BidStatus.Countered;

        // Partner-side context, for the customer's bid list.
        public string PartnerName { get; set; } = string.Empty;
        public string? PartnerSkillCategory { get; set; }
        public bool PartnerIsVerified { get; set; }

        // Task-side context, for the partner's bid list.
        public string? TaskCategoryName { get; set; }
        public string? TaskDescription { get; set; }
        public string? TaskStatus { get; set; }
        public decimal TaskBudget { get; set; }

        public static BidDto From(TaskBid bid) => new()
        {
            Id = bid.Id,
            GigTaskId = bid.GigTaskId,
            PartnerId = bid.PartnerId,
            Amount = bid.Amount,
            Note = bid.Note,
            CounterAmount = bid.CounterAmount,
            CounterNote = bid.CounterNote,
            Status = bid.Status,
            CreatedAt = bid.CreatedAt,
            UpdatedAt = bid.UpdatedAt,
            PartnerName = bid.Partner?.User?.Name ?? string.Empty,
            PartnerSkillCategory = bid.Partner?.SkillCategory?.Name,
            PartnerIsVerified = bid.Partner?.IsVerified ?? false,
            TaskCategoryName = bid.GigTask?.Category?.Name,
            TaskDescription = bid.GigTask?.Description,
            TaskStatus = bid.GigTask?.Status,
            TaskBudget = bid.GigTask?.Budget ?? 0m,
        };
    }

    public class BidResult
    {
        public bool Succeeded { get; init; }
        public string? Error { get; init; }
        public BidDto? Bid { get; init; }

        public static BidResult Ok(BidDto bid) => new() { Succeeded = true, Bid = bid };
        public static BidResult Fail(string error) => new() { Succeeded = false, Error = error };
    }
}
