using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class ServiceItemDto
    {
        public int Id { get; set; }
        public int SkillCategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>Partner payout, not the customer price.</summary>
        public decimal? BasePayout { get; set; }

        public bool AllowsInstantBooking { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int TaskCount { get; set; }
        public bool IsInUse => TaskCount > 0;

        /// <summary>Instant booking is meaningless without a payout to pay.</summary>
        public bool CanAllowInstant => BasePayout is > 0;

        public static ServiceItemDto From(ServiceItem item) => new()
        {
            Id = item.Id,
            SkillCategoryId = item.SkillCategoryId,
            CategoryName = item.SkillCategory?.Name ?? string.Empty,
            Name = item.Name,
            Description = item.Description,
            BasePayout = item.BasePayout,
            AllowsInstantBooking = item.AllowsInstantBooking,
            IsActive = item.IsActive,
            DisplayOrder = item.DisplayOrder,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
    }

    public class SaveServiceItemRequest
    {
        [Required(ErrorMessage = "Choose a category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
        [Display(Name = "Category")]
        public int SkillCategoryId { get; set; }

        [Required(ErrorMessage = "Enter a service name.")]
        [StringLength(80, MinimumLength = 2)]
        [Display(Name = "Service name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        [Range(0, 10_000_000)]
        [Display(Name = "Partner payout (₹)")]
        public decimal? BasePayout { get; set; }

        [Display(Name = "Allow instant booking")]
        public bool AllowsInstantBooking { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Range(0, 9999)]
        [Display(Name = "Display order")]
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// What completed work says a service item is really worth. Built from
    /// accepted bid amounts, not asking prices — asks are inflated for
    /// negotiation, so they would overstate the rate.
    /// </summary>
    public class PriceInsightDto
    {
        public int ServiceItemId { get; set; }
        public string ServiceItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;

        public int CompletedCount { get; set; }
        public decimal? MedianAmount { get; set; }
        public decimal? LowerQuartile { get; set; }
        public decimal? UpperQuartile { get; set; }
        public decimal? CurrentPayout { get; set; }

        /// <summary>Below this the median is noise, not a market rate.</summary>
        public const int MinimumSampleSize = 30;

        public bool HasEnoughData => CompletedCount >= MinimumSampleSize;

        /// <summary>
        /// A wide spread means the item covers jobs of very different sizes and
        /// should be split before it is priced.
        /// </summary>
        public decimal? SpreadRatio =>
            LowerQuartile is > 0 && UpperQuartile is not null
                ? Math.Round(UpperQuartile.Value / LowerQuartile.Value, 2)
                : null;

        public bool IsTooVaried => SpreadRatio is > 2.5m;
    }
}
