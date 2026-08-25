namespace GigApp.Api.Models
{
    /// <summary>
    /// Where a partner's KYC stands.
    ///
    /// This replaces a plain IsVerified boolean, which could not tell "never
    /// reviewed" apart from "reviewed and rejected" — both were false. An admin
    /// could not see that they had already rejected someone, and the partner was
    /// told their documents were "pending review" forever with no idea anything
    /// was wrong.
    /// </summary>
    public static class KycStatus
    {
        /// <summary>Documents are missing, so there is nothing to review yet.</summary>
        public const string NotSubmitted = "not_submitted";

        /// <summary>Everything is on file and waiting on an admin.</summary>
        public const string Pending = "pending";

        public const string Approved = "approved";

        /// <summary>Reviewed and turned down. The reason is on the partner.</summary>
        public const string Rejected = "rejected";

        public static readonly string[] All = { NotSubmitted, Pending, Approved, Rejected };

        public static bool IsValid(string? status) => status is not null && All.Contains(status);

        public static string Label(string status) => status switch
        {
            Approved => "Verified",
            Rejected => "Rejected",
            Pending => "Pending review",
            _ => "Not submitted",
        };

        public static string BadgeClass(string status) => status switch
        {
            Approved => "text-bg-success",
            Rejected => "text-bg-danger",
            Pending => "text-bg-warning",
            _ => "text-bg-secondary",
        };
    }
}
