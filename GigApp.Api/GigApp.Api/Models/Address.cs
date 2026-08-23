namespace GigApp.Api.Models
{
    /// <summary>
    /// A saved address belonging to a user. This is where latitude and longitude
    /// enter the system — without them a task cannot be matched to the nearest
    /// partner, so addresses and location are one job rather than two.
    ///
    /// Tasks reference an address rather than carrying free text, so a repeat
    /// booking does not mean retyping it and a moved pin fixes every future job.
    /// </summary>
    public class Address
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>"Home", "Office" — what the user calls it.</summary>
        public string Label { get; set; } = AddressLabel.Home;

        public string Line1 { get; set; } = string.Empty;
        public string? Line2 { get; set; }

        /// <summary>Flat or house number, kept apart so it can be shown to the partner separately.</summary>
        public string? HouseNumber { get; set; }

        /// <summary>Nearby landmark — in India this is often how the partner actually finds the place.</summary>
        public string? Landmark { get; set; }

        public string City { get; set; } = string.Empty;
        public string? State { get; set; }
        public string Pincode { get; set; } = string.Empty;

        /// <summary>
        /// Optional. A user may save an address without granting location access,
        /// and distance-based matching simply cannot use that address until a pin
        /// exists.
        /// </summary>
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// <summary>One per user, enforced when saving rather than by a constraint.</summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Soft delete. An address referenced by past tasks cannot be removed
        /// without losing where that work happened.
        /// </summary>
        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<GigTask> Tasks { get; set; } = new List<GigTask>();

        public bool HasCoordinates => Latitude is not null && Longitude is not null;

        /// <summary>Single-line form for tables and the partner's job card.</summary>
        public string ToSingleLine()
        {
            var parts = new[] { HouseNumber, Line1, Line2, Landmark, City, Pincode }
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return string.Join(", ", parts);
        }
    }

    public static class AddressLabel
    {
        public const string Home = "Home";
        public const string Office = "Office";
        public const string Other = "Other";

        public static readonly string[] All = { Home, Office, Other };

        public static bool IsValid(string? label) => label is not null && All.Contains(label);
    }
}
