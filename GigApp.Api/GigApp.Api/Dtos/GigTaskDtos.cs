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

        /// <summary>Required — this is the unit prices are discovered against.</summary>
        [Required(ErrorMessage = "Choose the service you need.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose the service you need.")]
        [Display(Name = "Service")]
        public int ServiceItemId { get; set; }

        [Required, StringLength(2000, MinimumLength = 5)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// A saved address. The server copies its text and coordinates onto the
        /// task, so callers never send an address as free text.
        /// </summary>
        [Required(ErrorMessage = "Choose where the work is needed.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose where the work is needed.")]
        [Display(Name = "Address")]
        public int AddressId { get; set; }

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

        /// <summary>Required when a partner moves a task to completed.</summary>
        public int Stars { get; set; }

        [StringLength(500)]
        public string? Feedback { get; set; }
    }

    public class GigTaskDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public int? ServiceItemId { get; set; }
        public string? ServiceItemName { get; set; }

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

        /// <summary>Filled only where the caller has a location to measure from.</summary>
        public double? DistanceKm { get; set; }
        public string DistanceLabel => Services.Geo.GeoPoint.Describe(DistanceKm);

        public string BookingMode { get; set; } = TaskBookingMode.Bidding;
        public bool IsInstant => BookingMode == TaskBookingMode.Instant;

        public DateTime? PreferredDateTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int? AddressId { get; set; }

        /// <summary>Snapshotted at booking, so it survives the address being edited.</summary>
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool HasCoordinates => Latitude is not null && Longitude is not null;

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public DateTime? AssignedAt { get; set; }
        public string? AssignedByName { get; set; }
        public string? AssignmentNote { get; set; }
        public bool WasAssignedBySupport => AssignedAt is not null;

        public int? PartnerId { get; set; }
        public string? PartnerName { get; set; }
        public string? PartnerSkillCategory { get; set; }
        public int? PartnerSkillCategoryId { get; set; }

        public bool PartnerChangedSkill =>
            PartnerSkillCategoryId is not null
            && PartnerSkillCategoryId != CategoryId
            && GigTaskStatus.IsOpen(Status);

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
            ServiceItemId = task.ServiceItemId,
            ServiceItemName = task.ServiceItem?.Name,
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
            AddressId = task.AddressId,
            Latitude = task.Latitude,
            Longitude = task.Longitude,
            CustomerId = task.CustomerId,
            CustomerName = task.Customer?.Name,
            AssignedAt = task.AssignedAt,
            AssignedByName = task.AssignedBy?.Name,
            AssignmentNote = task.AssignmentNote,
            PartnerId = task.PartnerId,
            PartnerName = task.Partner?.User?.Name,
            PartnerSkillCategory = task.Partner?.SkillCategory?.Name,
            PartnerSkillCategoryId = task.Partner?.SkillCategoryId,
        };
    }
}
