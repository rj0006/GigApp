namespace GigApp.Api.Models
{
    /// <summary>
    /// Task lifecycle: pending → accepted → in_progress → completed,
    /// with cancelled reachable from any non-terminal state.
    /// </summary>
    public static class GigTaskStatus
    {
        public const string Pending = "pending";
        public const string Accepted = "accepted";
        public const string InProgress = "in_progress";
        public const string Completed = "completed";
        public const string Cancelled = "cancelled";

        public static readonly string[] All =
            { Pending, Accepted, InProgress, Completed, Cancelled };

        /// <summary>Which statuses a task may move to from its current one.</summary>
        private static readonly Dictionary<string, string[]> Transitions = new()
        {
            [Pending] = new[] { Accepted, Cancelled },
            [Accepted] = new[] { InProgress, Cancelled },
            [InProgress] = new[] { Completed, Cancelled },
            [Completed] = Array.Empty<string>(),
            [Cancelled] = Array.Empty<string>(),
        };

        public static bool IsValid(string? status) =>
            status is not null && All.Contains(status);

        public static bool CanTransition(string from, string to) =>
            Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
