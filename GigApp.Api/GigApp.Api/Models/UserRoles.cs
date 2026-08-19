namespace GigApp.Api.Models
{
    /// <summary>
    /// Canonical role values. Always lowercase — role checks and JWT claims
    /// compare these verbatim, so casing drift silently breaks authorization.
    /// </summary>
    public static class UserRoles
    {
        public const string Customer = "customer";
        public const string Partner = "partner";
        public const string Admin = "admin";

        public static readonly string[] All = { Customer, Partner, Admin };

        public static bool IsValid(string? role) =>
            role is not null && All.Contains(role);
    }
}
