using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class MenuItemDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string? ParentLabel { get; set; }
        public string? ControllerName { get; set; }
        public string? ActionName { get; set; }
        public string? Url { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool OpensInNewTab { get; set; }
        public string Visibility { get; set; } = MenuVisibility.All;
        public string? BadgeKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsGroup =>
            string.IsNullOrWhiteSpace(Url)
            && string.IsNullOrWhiteSpace(ControllerName)
            && string.IsNullOrWhiteSpace(ActionName);

        public string VisibilityLabel => MenuVisibility.Label(Visibility);

        public string Target => IsGroup
            ? "—"
            : Url ?? $"{ControllerName}.{ActionName}";

        public static MenuItemDto From(MenuItem item) => new()
        {
            Id = item.Id,
            Label = item.Label,
            ParentId = item.ParentId,
            ParentLabel = item.Parent?.Label,
            ControllerName = item.ControllerName,
            ActionName = item.ActionName,
            Url = item.Url,
            Icon = item.Icon,
            SortOrder = item.SortOrder,
            IsActive = item.IsActive,
            OpensInNewTab = item.OpensInNewTab,
            Visibility = item.Visibility,
            BadgeKey = item.BadgeKey,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
    }

    public class MenuNodeDto
    {
        public MenuItemDto Item { get; set; } = new();
        public IReadOnlyList<MenuNodeDto> Children { get; set; } = Array.Empty<MenuNodeDto>();
    }

    public class MenuOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SaveMenuItemRequest
    {
        [Required, StringLength(60, MinimumLength = 2)]
        [Display(Name = "Label")]
        public string Label { get; set; } = string.Empty;

        [Display(Name = "Group")]
        public int? ParentId { get; set; }

        [StringLength(60)]
        [Display(Name = "Controller")]
        public string? ControllerName { get; set; }

        [StringLength(60)]
        [Display(Name = "Action")]
        public string? ActionName { get; set; }

        [StringLength(200)]
        [Display(Name = "URL")]
        public string? Url { get; set; }

        [StringLength(20)]
        [Display(Name = "Icon")]
        public string? Icon { get; set; }

        [Range(0, 999)]
        [Display(Name = "Sort order")]
        public int SortOrder { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Open in a new tab")]
        public bool OpensInNewTab { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Visible to")]
        public string Visibility { get; set; } = MenuVisibility.All;

        [StringLength(40)]
        [Display(Name = "Badge")]
        public string? BadgeKey { get; set; }

        public static SaveMenuItemRequest From(MenuItemDto item) => new()
        {
            Label = item.Label,
            ParentId = item.ParentId,
            ControllerName = item.ControllerName,
            ActionName = item.ActionName,
            Url = item.Url,
            Icon = item.Icon,
            SortOrder = item.SortOrder,
            IsActive = item.IsActive,
            OpensInNewTab = item.OpensInNewTab,
            Visibility = item.Visibility,
            BadgeKey = item.BadgeKey,
        };
    }
}
