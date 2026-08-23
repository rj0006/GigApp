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

        /// <summary>
        /// Sits above <see cref="Admin"/>. Everything an admin can do, plus the
        /// operations that must not be delegated — currently resetting another
        /// user's password and promoting or demoting administrators.
        ///
        /// A super admin is never created through a screen. One is seeded, and
        /// only an existing super admin can make another.
        /// </summary>
        public const string SuperAdmin = "superadmin";

        public static readonly string[] All = { Customer, Partner, Admin, SuperAdmin };

        /// <summary>Roles that reach the admin portal at all.</summary>
        public static readonly string[] AdminRoles = { Admin, SuperAdmin };

        /// <summary>Roles a super admin may assign through the portal.</summary>
        public static readonly string[] AssignableBySuperAdmin = { Admin, SuperAdmin };

        public static bool IsValid(string? role) =>
            role is not null && All.Contains(role);

        public static bool IsAdminRole(string? role) =>
            role is not null && AdminRoles.Contains(role);

        public static string Label(string role) => role switch
        {
            SuperAdmin => "Super admin",
            Admin => "Admin",
            Partner => "Partner",
            _ => "Customer",
        };
    }
}
