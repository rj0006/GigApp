namespace GigApp.Api.Models
{
    // Which categories are sellable inside one zone — not every category launches everywhere at once.
    public class ServiceZoneCategory
    {
        public int Id { get; set; }

        public int ServiceZoneId { get; set; }
        public ServiceZone? ServiceZone { get; set; }

        public int SkillCategoryId { get; set; }
        public SkillCategory? SkillCategory { get; set; }
    }
}
