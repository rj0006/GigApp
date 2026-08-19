namespace GigApp.Api.Models
{
    /// <summary>
    /// How soon the customer needs the work. Drives ordering on the partner's
    /// board today, and dispatch priority once instant booking exists.
    /// </summary>
    public static class TaskUrgency
    {
        /// <summary>Needed today — a leaking tap, no power.</summary>
        public const string Urgent = "urgent";

        /// <summary>Within the next few days. The default.</summary>
        public const string Normal = "normal";

        /// <summary>Whenever suits — the customer is not in a hurry.</summary>
        public const string Flexible = "flexible";

        public static readonly string[] All = { Urgent, Normal, Flexible };

        public static bool IsValid(string? urgency) =>
            urgency is not null && All.Contains(urgency);

        /// <summary>Lower sorts first, so urgent work surfaces at the top.</summary>
        public static int SortOrder(string urgency) => urgency switch
        {
            Urgent => 0,
            Normal => 1,
            _ => 2,
        };

        public static string Label(string urgency) => urgency switch
        {
            Urgent => "Urgent",
            Flexible => "Flexible",
            _ => "Normal",
        };

        /// <summary>Bootstrap badge class for the urgency chip.</summary>
        public static string BadgeClass(string urgency) => urgency switch
        {
            Urgent => "text-bg-danger",
            Flexible => "text-bg-secondary",
            _ => "text-bg-info",
        };
    }

    /// <summary>
    /// How a task gets its partner.
    ///
    /// Both modes exist on purpose. Standardised work is quicker to book at a
    /// fixed price, while custom work needs a quote — and the amounts that
    /// bidding settles on are what tell us the real market rate for a service,
    /// which is how the fixed prices get set in the first place.
    /// </summary>
    public static class TaskBookingMode
    {
        /// <summary>Partners quote, the customer picks one. Built and live.</summary>
        public const string Bidding = "bidding";

        /// <summary>
        /// Fixed catalogue price, partner assigned automatically. Reserved —
        /// it needs service-level pricing and partner location first.
        /// </summary>
        public const string Instant = "instant";

        public static readonly string[] All = { Bidding, Instant };

        public static bool IsValid(string? mode) => mode is not null && All.Contains(mode);
    }
}
