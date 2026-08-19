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
                e.Property(u => u.Role).HasMaxLength(20).IsRequired();

                // Both are login identifiers, so both must be unique. Email is
                // nullable — Postgres allows any number of NULLs in a unique index,
                // so phone-only accounts coexist fine.
                e.HasIndex(u => u.Email).IsUnique();
                e.HasIndex(u => u.Phone).IsUnique();

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_Users_Role",
                    "\"Role\" IN ('customer','partner','admin')"));
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
                e.HasOne(p => p.SkillCategory)
                 .WithMany(c => c.Partners)
                 .HasForeignKey(p => p.SkillCategoryId)
                 .OnDelete(DeleteBehavior.Restrict);

                // One profile per user; deleting the user removes the profile.
                e.HasOne(p => p.User)
                 .WithOne(u => u.PartnerProfile)
                 .HasForeignKey<Partner>(p => p.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(p => p.UserId).IsUnique();

                // Partner browsing filters on these two together.
                e.HasIndex(p => new { p.IsVerified, p.IsAvailable });
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
