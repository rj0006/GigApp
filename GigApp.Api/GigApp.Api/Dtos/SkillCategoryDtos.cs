using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class SkillCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageFileName { get; set; }
        public string? ImageUrl =>
            string.IsNullOrWhiteSpace(ImageFileName) ? null : $"/uploads/category/{ImageFileName}";
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>How many partners and tasks reference this category. Drives
        /// whether it can be hard-deleted; populated only where it is needed.</summary>
        public int PartnerCount { get; set; }
        public int TaskCount { get; set; }
        public bool IsInUse => PartnerCount > 0 || TaskCount > 0;

        public static SkillCategoryDto From(SkillCategory category) => new()
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ImageFileName = category.ImageFileName,
            IsActive = category.IsActive,
            DisplayOrder = category.DisplayOrder,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
        };
    }

    /// <summary>Minimal shape for autocomplete pickers.</summary>
    public class SkillCategoryOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SaveSkillCategoryRequest
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Enter a category name.")]
        [StringLength(60, MinimumLength = 2)]
        [Display(Name = "Category name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Range(0, 9999)]
        [Display(Name = "Display order")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Category image")]
        public IFormFile? Image { get; set; }
    }

    public class ToggleActiveRequest
    {
        public bool IsActive { get; set; }
    }
}
