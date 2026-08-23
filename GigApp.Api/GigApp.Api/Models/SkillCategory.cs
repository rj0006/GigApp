namespace GigApp.Api.Models
{
    /// <summary>
    /// Master list of service categories, maintained by admins. Both a partner's
    /// skill and a task's category point here — they must share one taxonomy or
    /// matching partners to tasks cannot work.
    /// </summary>
    public class SkillCategory
    {
        public int Id { get; set; }

        /// <summary>Display name, e.g. "Plumbing". Unique, compared case-insensitively.</summary>
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// Deactivating hides a category from new selections without invalidating
        /// the partners and tasks already using it.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Lower sorts first; ties fall back to name.</summary>
        public int DisplayOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Partner> Partners { get; set; } = new List<Partner>();
        public ICollection<GigTask> Tasks { get; set; } = new List<GigTask>();

        /// <summary>The specific jobs offered under this category.</summary>
        public ICollection<ServiceItem> ServiceItems { get; set; } = new List<ServiceItem>();
    }
}
