namespace GigApp.Api.Models
{
    /// <summary>
    /// Audit trail for every write that goes through a controller. One row per
    /// operation; <see cref="Payload"/> holds both the incoming request and the
    /// resulting record, so consecutive rows for the same DocNo can be compared
    /// to see what actually changed. There is deliberately no OldValues column —
    /// the previous row for that DocNo is the old value.
    /// </summary>
    public class TrackingLog
    {
        public long Id { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        /// <summary>insert | update | delete — see <see cref="TrackingEntryType"/>.</summary>
        public string EntryType { get; set; } = string.Empty;

        /// <summary>Module or entity the operation belongs to, e.g. "Partner".</summary>
        public string FormType { get; set; } = string.Empty;

        /// <summary>Record identifier as text. Null when the id could not be determined.</summary>
        public string? DocNo { get; set; }

        /// <summary>Business date of the record, when it has one.</summary>
        public DateTime? DocDate { get; set; }

        public int? UserId { get; set; }
        public string? UserName { get; set; }

        /// <summary>
        /// jsonb: <c>{ "request": {...}, "result": {...} }</c>. Sensitive keys
        /// (passwords, tokens) are replaced before this is written.
        /// </summary>
        public string Payload { get; set; } = "{}";

        /// <summary>
        /// Supplied by the user — the reason for an update or delete, a partner's
        /// completion feedback, or a cancellation reason.
        /// </summary>
        public string? Remark { get; set; }

        public string? IpAddress { get; set; }
        public string? RequestPath { get; set; }
    }

    public static class TrackingEntryType
    {
        public const string Insert = "insert";
        public const string Update = "update";
        public const string Delete = "delete";

        /// <summary>HTTP verb decides the entry type, so no per-endpoint wiring is needed.</summary>
        public static string FromHttpMethod(string method) => method.ToUpperInvariant() switch
        {
            "POST" => Insert,
            "PUT" or "PATCH" => Update,
            "DELETE" => Delete,
            _ => Update,
        };
    }
}
