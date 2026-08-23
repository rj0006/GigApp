namespace GigApp.Api.Models
{
    /// <summary>
    /// Identity and credentials for every actor in the system — customer,
    /// partner and admin alike. Role-specific data hangs off this row
    /// (see <see cref="PartnerProfile"/>).
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Primary identifier. Always required, and the account key once
        /// OTP verification lands.
        /// </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Optional secondary identifier — a phone-first signup may not have one.
        /// Unique when present (Postgres permits many NULLs in a unique index).
        /// </summary>
        public string? Email { get; set; }

        /// <summary>BCrypt hash. Never leaves the auth layer — DTOs must not expose it.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Flipped by the OTP flow. Nothing enforces it yet; once SMS is wired
        /// in, login and task acceptance should both require it.
        /// </summary>
        public bool IsPhoneVerified { get; set; } = false;
        public DateTime? PhoneVerifiedAt { get; set; }

        /// <summary>
        /// Stored file name only ("guid.jpg"). Served straight from wwwroot —
        /// a customer sees their partner's photo, so it is not private.
        /// </summary>
        public string? ProfileImageFileName { get; set; }

        public string Role { get; set; } = UserRoles.Customer;

        /// <summary>
        /// Deactivated accounts cannot sign in and existing tokens stop working
        /// on their next request. Deactivating is preferred over deleting: the
        /// user's tasks, bids and audit trail stay intact and readable.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime? DeactivatedAt { get; set; }

        /// <summary>Why the account was deactivated. Shown to admins only.</summary>
        public string? DeactivationReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Present only when <see cref="Role"/> is <c>partner</c>.</summary>
        public Partner? PartnerProfile { get; set; }
    }
}
