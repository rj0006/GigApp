namespace GigApp.Api.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        public string Label { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public MenuItem? Parent { get; set; }
        public ICollection<MenuItem> Children { get; set; } = new List<MenuItem>();

        public string? ControllerName { get; set; }
        public string? ActionName { get; set; }

        public string? Url { get; set; }

        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool OpensInNewTab { get; set; }

        public string Visibility { get; set; } = MenuVisibility.All;

        public string? BadgeKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool IsGroup =>
            string.IsNullOrWhiteSpace(Url)
            && string.IsNullOrWhiteSpace(ControllerName)
            && string.IsNullOrWhiteSpace(ActionName);
    }

    public static class MenuVisibility
    {
        public const string All = "all";
        public const string SuperAdmin = "super_admin";

        public static readonly string[] Options = { All, SuperAdmin };

        public static bool IsValid(string? value) => value is not null && Options.Contains(value);

        public static string Label(string value) =>
            value == SuperAdmin ? "Super admin only" : "Every administrator";
    }

    public static class MenuBadgeKeys
    {
        public const string PendingKyc = "pending_kyc";
        public const string OpenEnquiries = "open_enquiries";

        public static readonly string[] Options = { PendingKyc, OpenEnquiries };

        public static bool IsValid(string? value) =>
            string.IsNullOrWhiteSpace(value) || Options.Contains(value);
    }
}
