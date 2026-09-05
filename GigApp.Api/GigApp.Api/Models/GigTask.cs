namespace GigApp.Api.Models
{
    public class GigTask
    {
        public int Id { get; set; }

        // Who posted this task
        public int CustomerId { get; set; }
        public User? Customer { get; set; }

        // Who accepted it (nullable — no one has accepted yet when created)
        public int? PartnerId { get; set; }
        public Partner? Partner { get; set; }

        /// <summary>What kind of work this is — same master a partner's skill comes from.</summary>
        public int CategoryId { get; set; }
        public SkillCategory? Category { get; set; }

        /// <summary>
        /// The specific job within that category. Required for anything new —
        /// it is the unit prices are discovered and set against. Nullable only
        /// because tasks created before service items existed have none.
        /// </summary>
        public int? ServiceItemId { get; set; }
        public ServiceItem? ServiceItem { get; set; }

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Where the work happens. Kept as free text alongside the saved address
        /// so historic tasks stay readable and so a snapshot survives even if the
        /// customer later edits or deletes that address.
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// The saved address chosen at booking. Null for tasks created before
        /// addresses existed. Its coordinates are what distance matching uses.
        /// </summary>
        public int? AddressId { get; set; }
        public Address? BookingAddress { get; set; }

        /// <summary>
        /// Copied from the address at booking time. Held here as well so that
        /// moving a pin later does not silently relocate finished work.
        /// </summary>
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// <summary>
        /// What the customer expects to pay. Indicative only — partners bid
        /// against it, and the settled figure is <see cref="AgreedAmount"/>.
        /// Never use this for earnings or reporting.
        /// </summary>
        public decimal Budget { get; set; }

        /// <summary>
        /// The price actually agreed, taken from the winning bid. Null until a
        /// bid is accepted.
        /// </summary>
        public decimal? AgreedAmount { get; set; }

        public int? AcceptedBidId { get; set; }

        public ICollection<TaskBid> Bids { get; set; } = new List<TaskBid>();

        /// <summary>How soon this is needed — see <see cref="TaskUrgency"/>.</summary>
        public string Urgency { get; set; } = TaskUrgency.Normal;

        /// <summary>
        /// How the partner is chosen — see <see cref="TaskBookingMode"/>.
        /// Everything is bidding today; instant needs catalogue pricing and
        /// partner location before it can be switched on.
        /// </summary>
        public string BookingMode { get; set; } = TaskBookingMode.Bidding;

        public int? AssignedByUserId { get; set; }
        public User? AssignedBy { get; set; }
        public DateTime? AssignedAt { get; set; }
        public string? AssignmentNote { get; set; }

        public string Status { get; set; } = GigTaskStatus.Pending;
        // pending -> accepted -> in_progress -> completed -> cancelled

        public DateTime? PreferredDateTime { get; set; } // customer's requested time
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
