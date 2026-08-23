namespace GigApp.Api.Models
{
    /// <summary>
    /// A specific piece of work inside a category — "Tap repair" under Plumbing.
    ///
    /// Categories are too coarse to price: Plumbing covers a ₹200 washer change
    /// and a ₹15,000 re-pipe, and averaging those gives a meaningless number.
    /// Pricing and price discovery both need this finer unit, which is why one
    /// entity serves both.
    /// </summary>
    public class ServiceItem
    {
        public int Id { get; set; }

        public int SkillCategoryId { get; set; }
        public SkillCategory? SkillCategory { get; set; }

        /// <summary>Unique within its category, compared case-insensitively.</summary>
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// What the partner is paid for this job. Null until enough completed
        /// work exists to set it — see the note on payout vs customer price.
        ///
        /// This is the PARTNER PAYOUT, not what the customer is charged. The
        /// customer price is this plus platform commission; keeping them apart
        /// avoids repricing everything when commission changes.
        /// </summary>
        public decimal? BasePayout { get; set; }

        /// <summary>
        /// Whether this can be booked at a fixed price without bidding. Stays
        /// false until a payout is set and the work is standard enough.
        /// </summary>
        public bool AllowsInstantBooking { get; set; }

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<GigTask> Tasks { get; set; } = new List<GigTask>();
    }
}
