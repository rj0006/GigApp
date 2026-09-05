namespace GigApp.Api.Models
{
    public class TaskRating
    {
        public int Id { get; set; }

        public int GigTaskId { get; set; }
        public GigTask? GigTask { get; set; }

        public int RaterUserId { get; set; }
        public User? RaterUser { get; set; }

        public string RaterRole { get; set; } = string.Empty;

        public int Stars { get; set; }
        public string? Feedback { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class RatedBy
    {
        public const string Customer = UserRoles.Customer;
        public const string Partner = UserRoles.Partner;

        public static readonly string[] All = { Customer, Partner };

        public static bool IsValid(string? role) => role is not null && All.Contains(role);
    }

    public static class RatingScale
    {
        public const int Minimum = 1;
        public const int Maximum = 5;

        public static bool IsValid(int stars) => stars is >= Minimum and <= Maximum;

        public static string Label(int stars) => stars switch
        {
            5 => "Excellent",
            4 => "Good",
            3 => "Fair",
            2 => "Poor",
            1 => "Very poor",
            _ => "Not rated",
        };
    }
}
