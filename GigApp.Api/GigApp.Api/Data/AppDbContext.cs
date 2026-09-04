using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Partner> Partners => Set<Partner>();
        public DbSet<GigTask> GigTasks => Set<GigTask>();
        public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();
        public DbSet<TrackingLog> TrackingLogs => Set<TrackingLog>();
        public DbSet<TaskBid> TaskBids => Set<TaskBid>();
        public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(e =>
            {
                e.Property(u => u.Name).HasMaxLength(100).IsRequired();
                e.Property(u => u.Phone).HasMaxLength(20).IsRequired();
                e.Property(u => u.Email).HasMaxLength(256);
                e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
                e.Property(u => u.ProfileImageFileName).HasMaxLength(100);
                e.Property(u => u.DeactivationReason).HasMaxLength(300);
                e.HasIndex(u => new { u.Role, u.IsActive });
                e.Property(u => u.Role).HasMaxLength(20).IsRequired();

                e.HasIndex(u => new { u.Email, u.Role }).IsUnique();
                e.HasIndex(u => new { u.Phone, u.Role }).IsUnique();

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_Users_Role",
                    "\"Role\" IN ('customer','partner','admin','superadmin')"));
            });

            modelBuilder.Entity<BankAccount>(e =>
            {
                e.Property(a => a.AccountHolderName).HasMaxLength(100).IsRequired();
                e.Property(a => a.AccountNumber).HasMaxLength(18).IsRequired();
                e.Property(a => a.IfscCode).HasMaxLength(11).IsRequired();
                e.Property(a => a.BankName).HasMaxLength(100).IsRequired();
                e.Property(a => a.BranchName).HasMaxLength(100);
                e.Property(a => a.UpiId).HasMaxLength(100);
                e.Ignore(a => a.MaskedAccountNumber);
                e.HasIndex(a => a.UserId).IsUnique();
                e.HasOne(a => a.User).WithOne().HasForeignKey<BankAccount>(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Address>(e =>
            {
                e.Property(a => a.Label).HasMaxLength(20).IsRequired();
                e.Property(a => a.Line1).HasMaxLength(200).IsRequired();
                e.Property(a => a.Line2).HasMaxLength(200);
                e.Property(a => a.HouseNumber).HasMaxLength(50);
                e.Property(a => a.Landmark).HasMaxLength(150);
                e.Property(a => a.City).HasMaxLength(80).IsRequired();
                e.Property(a => a.State).HasMaxLength(80);
                e.Property(a => a.Pincode).HasMaxLength(6).IsRequired();

                e.HasOne(a => a.User)
                 .WithMany()
                 .HasForeignKey(a => a.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                // The address picker only ever wants a user's live addresses.
                e.HasIndex(a => new { a.UserId, a.IsDeleted });

                e.Ignore(a => a.HasCoordinates);

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_Addresses_Coordinates",
                    // Either both or neither — half a pin is not a location.
                    "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) "
                  + "OR (\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)"));
            });

            modelBuilder.Entity<ServiceItem>(e =>
            {
                e.Property(s => s.Name).HasMaxLength(80).IsRequired();
                e.Property(s => s.Description).HasMaxLength(300);
                e.Property(s => s.BasePayout).HasPrecision(10, 2);

                // Restrict, not Cascade — deleting a category must not silently
                // take its service items and their price history with it.
                e.HasOne(s => s.SkillCategory)
                 .WithMany(c => c.ServiceItems)
                 .HasForeignKey(s => s.SkillCategoryId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(s => new { s.SkillCategoryId, s.IsActive, s.DisplayOrder });
            });

            modelBuilder.Entity<TaskBid>(e =>
            {
                e.Property(b => b.Amount).HasPrecision(10, 2);
                e.Property(b => b.CounterAmount).HasPrecision(10, 2);
                e.Property(b => b.Note).HasMaxLength(500);
                e.Property(b => b.CounterNote).HasMaxLength(500);
                e.Property(b => b.Status).HasMaxLength(12).IsRequired();

                e.HasOne(b => b.GigTask)
                 .WithMany(t => t.Bids)
                 .HasForeignKey(b => b.GigTaskId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Removing a partner must not wipe the task's negotiation history.
                e.HasOne(b => b.Partner)
                 .WithMany()
                 .HasForeignKey(b => b.PartnerId)
                 .OnDelete(DeleteBehavior.Restrict);

                // One bid per partner per task — a partner edits their bid
                // rather than stacking up several.
                e.HasIndex(b => new { b.GigTaskId, b.PartnerId }).IsUnique();
                e.HasIndex(b => new { b.PartnerId, b.Status });

                e.Ignore(b => b.CurrentAmount);

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_TaskBids_Status",
                    "\"Status\" IN ('pending','countered','accepted','rejected','withdrawn')"));
            });

            modelBuilder.Entity<TrackingLog>(e =>
            {
                e.Property(t => t.EntryType).HasMaxLength(10).IsRequired();
                e.Property(t => t.FormType).HasMaxLength(60).IsRequired();
                e.Property(t => t.DocNo).HasMaxLength(60);
                e.Property(t => t.UserName).HasMaxLength(100);
                e.Property(t => t.Remark).HasMaxLength(1000);
                e.Property(t => t.IpAddress).HasMaxLength(45);   // fits IPv6
                e.Property(t => t.RequestPath).HasMaxLength(500);

                // jsonb, not text — lets us query inside the payload later,
                // e.g. Payload -> 'result' ->> 'phone'.
                e.Property(t => t.Payload).HasColumnType("jsonb").IsRequired();

                // "What did this user do" and "what happened to this record"
                // are the two questions this table gets asked.
                e.HasIndex(t => new { t.UserId, t.TransactionDate });
                e.HasIndex(t => new { t.FormType, t.DocNo });
                e.HasIndex(t => t.TransactionDate);

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_TrackingLogs_EntryType",
                    "\"EntryType\" IN ('insert','update','delete')"));
            });

            modelBuilder.Entity<SkillCategory>(e =>
            {
                e.Property(c => c.Name).HasMaxLength(60).IsRequired();
                e.Property(c => c.Description).HasMaxLength(300);

                // Case-insensitive uniqueness is enforced by a functional index
                // created in the migration; this one keeps lookups fast.
                e.HasIndex(c => c.Name);
                e.HasIndex(c => new { c.IsActive, c.DisplayOrder });
            });

            modelBuilder.Entity<Partner>(e =>
            {
                e.Property(p => p.SelfieFileName).HasMaxLength(100);
                e.Property(p => p.AadhaarFrontFileName).HasMaxLength(100);
                e.Property(p => p.AadhaarBackFileName).HasMaxLength(100);
                e.Property(p => p.AadhaarNumber).HasMaxLength(12);

                // Ignore the computed helper; it is derived, not stored.
                e.Ignore(p => p.HasCompleteKyc);

                // Restrict, not Cascade — a category in use must be deactivated
                // rather than deleted out from under its partners.
                e.Property(p => p.BaseCity).HasMaxLength(80);
                e.Property(p => p.BasePincode).HasMaxLength(6);
                e.Ignore(p => p.HasServiceArea);

                e.HasOne(p => p.SkillCategory)
                 .WithMany(c => c.Partners)
                 .HasForeignKey(p => p.SkillCategoryId)
                 .OnDelete(DeleteBehavior.Restrict);

                // One profile per user; deleting the user removes the profile.
                e.HasOne(p => p.User)
                 .WithOne(u => u.PartnerProfile)
                 .HasForeignKey<Partner>(p => p.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(p => p.KycStatus).HasMaxLength(15).IsRequired();
                e.Property(p => p.KycRejectionReason).HasMaxLength(500);
                e.Property(p => p.KycReviewNote).HasMaxLength(300);

                // IsVerified is derived from KycStatus, not stored.
                e.Ignore(p => p.IsVerified);

                e.HasIndex(p => p.UserId).IsUnique();

                // Partner browsing filters on these two together.
                e.HasIndex(p => new { p.KycStatus, p.IsAvailable });

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_Partners_KycStatus",
                    "\"KycStatus\" IN ('not_submitted','pending','approved','rejected')"));
            });

            modelBuilder.Entity<GigTask>(e =>
            {
                e.Property(t => t.Description).HasMaxLength(2000).IsRequired();
                e.Property(t => t.Address).HasMaxLength(500).IsRequired();
                e.Property(t => t.Status).HasMaxLength(20).IsRequired();
                e.Property(t => t.Urgency).HasMaxLength(10).IsRequired();
                e.Property(t => t.BookingMode).HasMaxLength(10).IsRequired();
                e.Property(t => t.Budget).HasPrecision(10, 2);
                e.Property(t => t.AgreedAmount).HasPrecision(10, 2);

                e.HasOne(t => t.Category)
                 .WithMany(c => c.Tasks)
                 .HasForeignKey(t => t.CategoryId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(t => t.ServiceItem)
                 .WithMany(s => s.Tasks)
                 .HasForeignKey(t => t.ServiceItemId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Restrict, not Cascade — deleting an address must never take
                // the customer's task history with it. Addresses soft-delete.
                e.HasOne(t => t.BookingAddress)
                 .WithMany(a => a.Tasks)
                 .HasForeignKey(t => t.AddressId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Price discovery groups completed work by service item.
                e.HasIndex(t => new { t.ServiceItemId, t.Status });

                e.HasOne(t => t.Customer)
                 .WithMany()
                 .HasForeignKey(t => t.CustomerId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Removing a partner must not erase the customer's task history.
                e.HasOne(t => t.Partner)
                 .WithMany()
                 .HasForeignKey(t => t.PartnerId)
                 .OnDelete(DeleteBehavior.SetNull);

                // The "available tasks" feed filters on Status and sorts by CreatedAt.
                e.HasIndex(t => new { t.Status, t.CreatedAt });

                // Urgent work should surface first on the partner's board.
                e.HasIndex(t => new { t.Status, t.Urgency });

                e.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_GigTasks_Status",
                        "\"Status\" IN ('pending','accepted','in_progress','completed','cancelled')");

                    t.HasCheckConstraint(
                        "CK_GigTasks_Urgency",
                        "\"Urgency\" IN ('urgent','normal','flexible')");

                    t.HasCheckConstraint(
                        "CK_GigTasks_BookingMode",
                        "\"BookingMode\" IN ('bidding','instant')");
                });
            });
        }
    }
}
