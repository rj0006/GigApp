namespace GigApp.Api.Models
{
    /// <summary>
    /// Service-provider profile. Name, phone, email and credentials live on the
    /// linked <see cref="User"/> — this holds only what makes a partner a partner.
    /// </summary>
    public class Partner
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>What this partner does — drawn from the admin-managed master.</summary>
        public int SkillCategoryId { get; set; }
        public SkillCategory? SkillCategory { get; set; }

        /// <summary>Set by an admin after KYC review. Unverified partners cannot accept tasks.</summary>
        public bool IsVerified { get; set; } = false;

        /// <summary>Partner's own on/off toggle for receiving work.</summary>
        public bool IsAvailable { get; set; } = true;

        // ------------------------------------------------------------- KYC
        // Stored file names only ("guid.jpg"), never a path or URL. All three
        // live outside wwwroot and are served by FilesController after an
        // ownership check — they are identity documents.
        //
        // Nullable in the database because partners created before KYC existed
        // have none; new registrations require all of them, and an admin cannot
        // approve a partner until every one is present.

        /// <summary>Photo of the partner, to match against the Aadhaar card.</summary>
        public string? SelfieFileName { get; set; }

        public string? AadhaarFrontFileName { get; set; }
        public string? AadhaarBackFileName { get; set; }

        /// <summary>12-digit Aadhaar number. Masked in the audit trail.</summary>
        public string? AadhaarNumber { get; set; }

        /// <summary>An admin may only approve once every document is on file.</summary>
        public bool HasCompleteKyc =>
            !string.IsNullOrWhiteSpace(SelfieFileName)
            && !string.IsNullOrWhiteSpace(AadhaarFrontFileName)
            && !string.IsNullOrWhiteSpace(AadhaarBackFileName)
            && !string.IsNullOrWhiteSpace(AadhaarNumber);

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
