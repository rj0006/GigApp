using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class CreateGigTaskRequest
    {
        [Required(ErrorMessage = "Choose a category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required, StringLength(2000, MinimumLength = 5)]
        public string Description { get; set; } = string.Empty;

        [Required, StringLength(500, MinimumLength = 5)]
        public string Address { get; set; } = string.Empty;

        [Range(0.0, 10_000_000.0)]
        public decimal Budget { get; set; }

        /// <summary>urgent | normal | flexible. Defaults to normal when omitted.</summary>
        [Display(Name = "How soon?")]
        public string Urgency { get; set; } = TaskUrgency.Normal;

        public DateTime? PreferredDateTime { get; set; }
    }

    public class UpdateGigTaskStatusRequest
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    public class GigTaskDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        /// <summary>Customer's expected budget — indicative only.</summary>
        public decimal Budget { get; set; }

        /// <summary>Price settled from the winning bid; null until one is accepted.</summary>
        public decimal? AgreedAmount { get; set; }

        /// <summary>What to show as the price: the agreed figure once there is one.</summary>
        public decimal EffectiveAmount => AgreedAmount ?? Budget;

        public string Status { get; set; } = string.Empty;

        public string Urgency { get; set; } = TaskUrgency.Normal;
        public string UrgencyLabel => TaskUrgency.Label(Urgency);
        public string UrgencyBadgeClass => TaskUrgency.BadgeClass(Urgency);
        public bool IsUrgent => Urgency == TaskUrgency.Urgent;

        public string BookingMode { get; set; } = TaskBookingMode.Bidding;

        public DateTime? PreferredDateTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int? PartnerId { get; set; }
        public string? PartnerName { get; set; }
        public string? PartnerSkillCategory { get; set; }

        public string StatusLabel => Status.Replace('_', ' ');

        /// <summary>
        /// Customer and Partner are only populated when the query Included them;
        /// the name fields stay null otherwise rather than triggering lazy loads.
        /// </summary>
        public static GigTaskDto From(GigTask task) => new()
        {
            Id = task.Id,
            CategoryId = task.CategoryId,
            CategoryName = task.Category?.Name ?? string.Empty,
            Description = task.Description,
            Address = task.Address,
            Budget = task.Budget,
            AgreedAmount = task.AgreedAmount,
            Status = task.Status,
            Urgency = task.Urgency,
            BookingMode = task.BookingMode,
            PreferredDateTime = task.PreferredDateTime,
            CreatedAt = task.CreatedAt,
            CompletedAt = task.CompletedAt,
            CustomerId = task.CustomerId,
            CustomerName = task.Customer?.Name,
            PartnerId = task.PartnerId,
            PartnerName = task.Partner?.User?.Name,
            PartnerSkillCategory = task.Partner?.SkillCategory?.Name,
        };
    }
}
