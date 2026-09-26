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
        public DbSet<TaskRating> TaskRatings => Set<TaskRating>();
        public DbSet<SupportEnquiry> SupportEnquiries => Set<SupportEnquiry>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<TaskOffer> TaskOffers => Set<TaskOffer>();
        public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
        public DbSet<MenuItem> MenuItems => Set<MenuItem>();
        public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
        public DbSet<PartnerWallet> PartnerWallets => Set<PartnerWallet>();
        public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
        public DbSet<CommissionPlan> CommissionPlans => Set<CommissionPlan>();
        public DbSet<PartnerPlanSubscription> PartnerPlanSubscriptions => Set<PartnerPlanSubscription>();
        public DbSet<TaxRule> TaxRules => Set<TaxRule>();
        public DbSet<Banner> Banners => Set<Banner>();
        public DbSet<TaskCancellation> TaskCancellations => Set<TaskCancellation>();
        public DbSet<CartItem> CartItems => Set<CartItem>();
        public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
        public DbSet<AuthSettings> AuthSettings => Set<AuthSettings>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
                e.Property(u => u.AverageRating).HasPrecision(3, 2);

                e.HasIndex(u => new { u.Email, u.Role }).IsUnique();
                e.HasIndex(u => new { u.Phone, u.Role }).IsUnique();

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_Users_Role",
                    "\"Role\" IN ('customer','partner','admin','superadmin')"));
            });

            modelBuilder.Entity<CommissionPlan>(e =>
            {
                e.Property(p => p.Name).HasMaxLength(80).IsRequired();
                e.Property(p => p.Code).HasMaxLength(30).IsRequired();
                e.Property(p => p.Description).HasMaxLength(400);
                e.Property(p => p.CommissionPercent).HasPrecision(5, 2);
                e.Property(p => p.SubscriptionFee).HasPrecision(10, 2);
                e.Property(p => p.BillingPeriod).HasMaxLength(20).IsRequired();
                e.Ignore(p => p.IsZeroCommission);
                e.Ignore(p => p.IsSubscription);
                e.HasIndex(p => p.Code).IsUnique();
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_CommissionPlans_Percent",
                        "\"CommissionPercent\" >= 0 AND \"CommissionPercent\" <= 100");
                    t.HasCheckConstraint("CK_CommissionPlans_Fee", "\"SubscriptionFee\" >= 0");
                });
            });

            modelBuilder.Entity<PartnerPlanSubscription>(e =>
            {
                e.Property(s => s.CommissionPercent).HasPrecision(5, 2);
                e.Property(s => s.FeeCharged).HasPrecision(10, 2);
                e.Property(s => s.Remark).HasMaxLength(300);
                e.Property(s => s.AssignedByName).HasMaxLength(100);
                e.Ignore(s => s.HasExpired);
                e.HasIndex(s => new { s.PartnerId, s.IsActive });
                e.HasOne(s => s.Partner).WithMany().HasForeignKey(s => s.PartnerId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(s => s.CommissionPlan).WithMany(p => p.Subscriptions)
                    .HasForeignKey(s => s.CommissionPlanId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TaxRule>(e =>
            {
                e.Property(t => t.Name).HasMaxLength(80).IsRequired();
                e.Property(t => t.Code).HasMaxLength(30).IsRequired();
                e.Property(t => t.CountryCode).HasMaxLength(2).IsRequired();
                e.Property(t => t.AppliesTo).HasMaxLength(30).IsRequired();
                e.Property(t => t.Percent).HasPrecision(5, 2);
                e.Property(t => t.ThresholdAmount).HasPrecision(12, 2);
                e.Property(t => t.Note).HasMaxLength(400);
                e.HasIndex(t => new { t.CountryCode, t.Code }).IsUnique();
                e.HasIndex(t => new { t.CountryCode, t.IsActive, t.SortOrder });
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_TaxRules_Percent",
                        "\"Percent\" >= 0 AND \"Percent\" <= 100");
                    t.HasCheckConstraint("CK_TaxRules_AppliesTo",
                        "\"AppliesTo\" IN ('commission','gross_earning','subscription_fee')");
                });
            });

            modelBuilder.Entity<ErrorLog>(e =>
            {
                e.Property(l => l.Reference).HasMaxLength(20).IsRequired();
                e.Property(l => l.Message).HasMaxLength(2000).IsRequired();
                e.Property(l => l.ExceptionType).HasMaxLength(200).IsRequired();
                e.Property(l => l.StackTrace).HasMaxLength(8000);
                e.Property(l => l.InnerMessage).HasMaxLength(2000);
                e.Property(l => l.Module).HasMaxLength(200);
                e.Property(l => l.RequestPath).HasMaxLength(400);
                e.Property(l => l.RequestMethod).HasMaxLength(10);
                e.Property(l => l.UserName).HasMaxLength(100);
                e.Property(l => l.IpAddress).HasMaxLength(45);
                e.Property(l => l.ResolutionNote).HasMaxLength(500);
                e.HasIndex(l => l.Reference).IsUnique();
                e.HasIndex(l => new { l.IsResolved, l.OccurredAt });
            });

            modelBuilder.Entity<PartnerWallet>(e =>
            {
                e.Property(w => w.Balance).HasPrecision(12, 2);
                e.Property(w => w.LifetimeEarned).HasPrecision(12, 2);
                e.Property(w => w.LifetimeCommission).HasPrecision(12, 2);
                e.Property(w => w.LifetimeTax).HasPrecision(12, 2);
                e.Property(w => w.LifetimePaidOut).HasPrecision(12, 2);
                e.HasIndex(w => w.PartnerId).IsUnique();
                e.HasOne(w => w.Partner).WithOne().HasForeignKey<PartnerWallet>(w => w.PartnerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LedgerEntry>(e =>
            {
                e.Property(l => l.EntryType).HasMaxLength(30).IsRequired();
                e.Property(l => l.Direction).HasMaxLength(10).IsRequired();
                e.Property(l => l.Amount).HasPrecision(12, 2);
                e.Property(l => l.BalanceAfter).HasPrecision(12, 2);
                e.Property(l => l.Description).HasMaxLength(200).IsRequired();
                e.Property(l => l.IdempotencyKey).HasMaxLength(120).IsRequired();
                e.Property(l => l.Reference).HasMaxLength(60);
                e.Property(l => l.Remark).HasMaxLength(300);
                e.Property(l => l.CreatedByName).HasMaxLength(100);
                e.HasIndex(l => l.IdempotencyKey).IsUnique();
                e.HasIndex(l => new { l.PartnerId, l.CreatedAt });
                e.HasOne(l => l.Partner).WithMany().HasForeignKey(l => l.PartnerId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(l => l.GigTask).WithMany().HasForeignKey(l => l.GigTaskId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(l => l.TaxRule).WithMany().HasForeignKey(l => l.TaxRuleId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(l => l.CommissionPlan).WithMany().HasForeignKey(l => l.CommissionPlanId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.Property(l => l.AppliedPercent).HasPrecision(5, 2);
                e.Property(l => l.BaseAmount).HasPrecision(12, 2);
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_LedgerEntries_Direction",
                        "\"Direction\" IN ('credit','debit')");
                    t.HasCheckConstraint("CK_LedgerEntries_Amount", "\"Amount\" > 0");
                });
            });

            modelBuilder.Entity<MenuItem>(e =>
            {
                e.Property(m => m.Label).HasMaxLength(60).IsRequired();
                e.Property(m => m.ControllerName).HasMaxLength(60);
                e.Property(m => m.ActionName).HasMaxLength(60);
                e.Property(m => m.Url).HasMaxLength(200);
                e.Property(m => m.Icon).HasMaxLength(20);
                e.Property(m => m.Visibility).HasMaxLength(20).IsRequired();
                e.Property(m => m.BadgeKey).HasMaxLength(40);
                e.Ignore(m => m.IsGroup);
                e.HasIndex(m => new { m.ParentId, m.SortOrder });
                e.HasOne(m => m.Parent).WithMany(m => m.Children).HasForeignKey(m => m.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.ToTable(t => t.HasCheckConstraint(
                    "CK_MenuItems_Visibility",
                    "\"Visibility\" IN ('all','super_admin')"));
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

                e.Property(p => p.BaseLocation).HasColumnType("geography (point, 4326)");
                e.HasIndex(p => p.BaseLocation).HasMethod("gist");

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

            modelBuilder.Entity<TaskRating>(e =>
            {
                e.Property(r => r.RaterRole).HasMaxLength(20).IsRequired();
                e.Property(r => r.Feedback).HasMaxLength(500);

                e.HasOne(r => r.GigTask)
                 .WithMany(t => t.Ratings)
                 .HasForeignKey(r => r.GigTaskId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(r => r.RaterUser)
                 .WithMany()
                 .HasForeignKey(r => r.RaterUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(r => new { r.GigTaskId, r.RaterRole }).IsUnique();

                e.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_TaskRatings_Stars", "\"Stars\" BETWEEN 1 AND 5");

                    t.HasCheckConstraint(
                        "CK_TaskRatings_RaterRole", "\"RaterRole\" IN ('customer','partner')");
                });
            });

            modelBuilder.Entity<Banner>(e =>
            {
                e.Property(b => b.Title).HasMaxLength(120).IsRequired();
                e.Property(b => b.Subtitle).HasMaxLength(200);
                e.Property(b => b.CallToAction).HasMaxLength(40);
                e.Property(b => b.LinkUrl).HasMaxLength(300);
                e.Property(b => b.Placement).HasMaxLength(20).IsRequired();

                e.HasIndex(b => new { b.Placement, b.IsActive, b.SortOrder });

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_Banners_Placement", "\"Placement\" IN ('spotlight','wide')"));
            });

            modelBuilder.Entity<Notification>(e =>
            {
                e.Property(n => n.Type).HasMaxLength(30).IsRequired();
                e.Property(n => n.Title).HasMaxLength(150).IsRequired();
                e.Property(n => n.Body).HasMaxLength(500).IsRequired();
                e.Property(n => n.Link).HasMaxLength(300);

                e.HasOne(n => n.User)
                 .WithMany()
                 .HasForeignKey(n => n.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                // The bell asks "how many unread for me" on every page.
                e.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            });

            modelBuilder.Entity<TaskOffer>(e =>
            {
                e.Property(o => o.Status).HasMaxLength(20).IsRequired();

                e.HasOne(o => o.GigTask)
                 .WithMany(t => t.Offers)
                 .HasForeignKey(o => o.GigTaskId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(o => o.Partner)
                 .WithMany()
                 .HasForeignKey(o => o.PartnerId)
                 .OnDelete(DeleteBehavior.Cascade);

                // One offer per partner per task, so a chain can never loop back
                // to somebody who has already passed.
                e.HasIndex(o => new { o.GigTaskId, o.PartnerId }).IsUnique();

                // The sweep asks for pending offers that are past their expiry.
                e.HasIndex(o => new { o.Status, o.ExpiresAt });

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_TaskOffers_Status",
                    "\"Status\" IN ('pending','accepted','declined','expired')"));
            });

            modelBuilder.Entity<TaskCancellation>(e =>
            {
                e.Property(c => c.Reason).HasMaxLength(500);

                e.HasOne(c => c.GigTask)
                 .WithMany()
                 .HasForeignKey(c => c.GigTaskId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(c => c.Partner)
                 .WithMany()
                 .HasForeignKey(c => c.PartnerId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(c => new { c.GigTaskId, c.PartnerId });
            });

            modelBuilder.Entity<CartItem>(e =>
            {
                e.HasOne(c => c.Customer)
                 .WithMany()
                 .HasForeignKey(c => c.CustomerId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(c => c.ServiceItem)
                 .WithMany()
                 .HasForeignKey(c => c.ServiceItemId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(c => new { c.CustomerId, c.ServiceItemId }).IsUnique();
            });

            modelBuilder.Entity<OtpChallenge>(e =>
            {
                e.Property(o => o.Phone).HasMaxLength(20).IsRequired();
                e.Property(o => o.Role).HasMaxLength(20).IsRequired();
                e.Property(o => o.CodeHash).HasMaxLength(100).IsRequired();

                e.HasIndex(o => new { o.Phone, o.Role, o.CreatedAt });
            });

            modelBuilder.Entity<AuthSettings>(e =>
            {
                e.Property(a => a.CustomerLoginMode).HasMaxLength(20).IsRequired();
                e.Property(a => a.PartnerLoginMode).HasMaxLength(20).IsRequired();
            });

            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.Property(r => r.TokenHash).HasMaxLength(88).IsRequired();
                e.Property(r => r.DeviceLabel).HasMaxLength(100);
                e.Property(r => r.IpAddress).HasMaxLength(45);

                e.HasOne(r => r.User)
                 .WithMany()
                 .HasForeignKey(r => r.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(r => r.TokenHash).IsUnique();

                // Manage devices lists a user's own live sessions.
                e.HasIndex(r => new { r.UserId, r.RevokedAt, r.ExpiresAt });
            });

            modelBuilder.Entity<SupportEnquiry>(e =>
            {
                e.Property(s => s.RaisedByRole).HasMaxLength(20).IsRequired();
                e.Property(s => s.Topic).HasMaxLength(30).IsRequired();
                e.Property(s => s.Message).HasMaxLength(1000).IsRequired();
                e.Property(s => s.Status).HasMaxLength(20).IsRequired();
                e.Property(s => s.Resolution).HasMaxLength(1000);

                e.HasOne(s => s.GigTask)
                 .WithMany(t => t.Enquiries)
                 .HasForeignKey(s => s.GigTaskId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(s => s.RaisedByUser)
                 .WithMany()
                 .HasForeignKey(s => s.RaisedByUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(s => s.ResolvedByUser)
                 .WithMany()
                 .HasForeignKey(s => s.ResolvedByUserId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(s => new { s.Status, s.CreatedAt });
                e.HasIndex(s => new { s.GigTaskId, s.RaisedByUserId });

                e.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_SupportEnquiries_Status",
                        "\"Status\" IN ('open','in_progress','resolved')");

                    t.HasCheckConstraint(
                        "CK_SupportEnquiries_Topic",
                        "\"Topic\" IN ('payment','quality','behaviour','timing','other')");

                    t.HasCheckConstraint(
                        "CK_SupportEnquiries_RaisedByRole",
                        "\"RaisedByRole\" IN ('customer','partner')");
                });
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
                e.Property(t => t.Quantity).HasDefaultValue(1);

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

                e.Property(t => t.AssignmentNote).HasMaxLength(300);

                e.HasOne(t => t.AssignedBy)
                 .WithMany()
                 .HasForeignKey(t => t.AssignedByUserId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.Property(t => t.Location).HasColumnType("geography (point, 4326)");

                // Matching asks "which open tasks are near this partner", so the
                // spatial index only earns its keep alongside the status filter.
                e.HasIndex(t => t.Location).HasMethod("gist");

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
