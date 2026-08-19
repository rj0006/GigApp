namespace GigApp.Api.Models
{
    /// <summary>
    /// A partner's offer to do a task, and the negotiation around it.
    ///
    /// A task is no longer claimed first-come-first-served — partners bid, the
    /// customer picks one, and only then is the partner assigned. Either side
    /// can be the one who accepts: the customer accepts <see cref="Amount"/>,
    /// or the partner accepts the customer's <see cref="CounterAmount"/>.
    /// </summary>
    public class TaskBid
    {
        public int Id { get; set; }

        public int GigTaskId { get; set; }
        public GigTask? GigTask { get; set; }

        public int PartnerId { get; set; }
        public Partner? Partner { get; set; }

        /// <summary>What the partner is asking for.</summary>
        public decimal Amount { get; set; }

        /// <summary>Partner's short pitch to the customer.</summary>
        public string? Note { get; set; }

        /// <summary>What the customer offered back, if they countered.</summary>
        public decimal? CounterAmount { get; set; }
        public string? CounterNote { get; set; }

        public string Status { get; set; } = BidStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>The figure that applies right now — the counter if there is one.</summary>
        public decimal CurrentAmount => CounterAmount ?? Amount;
    }

    /// <summary>
    /// Bid lifecycle. <c>countered</c> is the only state where the ball is back
    /// in the partner's court.
    /// </summary>
    public static class BidStatus
    {
        /// <summary>Partner has bid; waiting on the customer.</summary>
        public const string Pending = "pending";

        /// <summary>Customer proposed a different amount; waiting on the partner.</summary>
        public const string Countered = "countered";

        public const string Accepted = "accepted";

        /// <summary>Customer declined, or another bid won this task.</summary>
        public const string Rejected = "rejected";

        /// <summary>Partner pulled out.</summary>
        public const string Withdrawn = "withdrawn";

        public static readonly string[] All =
            { Pending, Countered, Accepted, Rejected, Withdrawn };

        /// <summary>Still in play — these block a duplicate bid and can still be acted on.</summary>
        public static readonly string[] Open = { Pending, Countered };

        public static bool IsValid(string? status) => status is not null && All.Contains(status);
        public static bool IsOpen(string status) => Open.Contains(status);
    }
}
