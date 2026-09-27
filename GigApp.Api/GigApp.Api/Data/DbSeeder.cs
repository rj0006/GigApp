using GigApp.Api.Models;
using GigApp.Api.Services.Geo;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Data
{
    /// <summary>
    /// Development-only seeding. Every step is idempotent, so it is safe to run
    /// on each startup — it only fills gaps it finds.
    /// </summary>
    public static class DbSeeder
    {
        /// <summary>Dev credential for seeded accounts. Never used outside Development.</summary>
        public const string DevPassword = "Gigapp@123";

        /// <summary>
        /// Baseline categories so a fresh database is usable immediately. Admins
        /// manage the list from Masters → Skill Categories after this.
        /// </summary>
        private static readonly (string Name, string Description)[] DefaultCategories =
        {
            ("Plumbing",          "Taps, pipes, leaks, bathroom and kitchen fittings"),
            ("Electrical",        "Wiring, switches, fans, lighting and fault finding"),
            ("Cleaning",          "Deep cleaning for homes, kitchens and bathrooms"),
            ("Shifting",          "Packing, loading and household relocation"),
            ("Carpentry",         "Furniture assembly, repairs and woodwork"),
            ("Painting",          "Interior and exterior wall painting"),
            ("Appliance Repair",  "AC, refrigerator, washing machine and geyser service"),
            ("Pest Control",      "Cockroach, termite and mosquito treatment"),
        };

        /// <summary>
        /// Starter service items per category. Categories are too coarse to
        /// price, so every category needs at least a few of these before a task
        /// can be posted against it.
        /// </summary>
        private static readonly Dictionary<string, string[]> DefaultServiceItems = new()
        {
            ["Plumbing"] = new[] { "Tap repair", "Leak fix", "New fitting installation", "Drain unclogging" },
            ["Electrical"] = new[] { "Switch or socket repair", "Fan installation", "Light fitting", "Wiring fault check" },
            ["Cleaning"] = new[] { "1BHK deep clean", "2BHK deep clean", "Bathroom deep clean", "Sofa cleaning" },
            ["Shifting"] = new[] { "1BHK shifting", "2BHK shifting", "Single item shifting" },
            ["Carpentry"] = new[] { "Furniture assembly", "Door repair", "Cupboard repair" },
            ["Painting"] = new[] { "Single room painting", "Full house painting", "Touch-up work" },
            ["Appliance Repair"] = new[] { "AC service", "Refrigerator repair", "Washing machine repair", "Geyser repair" },
            ["Pest Control"] = new[] { "Cockroach treatment", "Termite treatment", "Mosquito treatment" },
            ["General"] = new[] { "General help" },
        };

        public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken ct = default)
        {
            await SeedCategoriesAsync(context, logger, ct);
            await SeedServiceItemsAsync(context, logger, ct);
            await SeedAccountsAsync(context, logger, ct);
            await SeedMenuAsync(context, logger, ct);
            await SeedPlansAndTaxesAsync(context, logger, ct);
            await SeedAuthSettingsAsync(context, logger, ct);
            await SeedServiceZonesAsync(context, logger, ct);
        }

        private static async Task SeedAuthSettingsAsync(AppDbContext context, ILogger logger, CancellationToken ct)
        {
            if (await context.AuthSettings.AnyAsync(ct)) return;

            context.AuthSettings.Add(new AuthSettings());
            await context.SaveChangesAsync(ct);
            logger.LogInformation("Seeded default login-mode settings (OTP for customer and partner)");
        }

        private static async Task SeedPlansAndTaxesAsync(
            AppDbContext context, ILogger logger, CancellationToken ct)
        {
            if (!await context.CommissionPlans.AnyAsync(ct))
            {
                context.CommissionPlans.AddRange(
                    new CommissionPlan
                    {
                        Name = "Standard",
                        Code = "STD",
                        Description = "No fee to join. The platform keeps a share of every job.",
                        CommissionPercent = 15m,
                        SubscriptionFee = 0m,
                        BillingPeriod = PlanBillingPeriod.None,
                        IsDefault = true,
                        DisplayOrder = 1,
                    },
                    new CommissionPlan
                    {
                        Name = "Lite",
                        Code = "LITE",
                        Description = "A small monthly fee brings the commission down.",
                        CommissionPercent = 8m,
                        SubscriptionFee = 499m,
                        BillingPeriod = PlanBillingPeriod.Monthly,
                        DisplayOrder = 2,
                    },
                    new CommissionPlan
                    {
                        Name = "Zero commission",
                        Code = "ZERO",
                        Description = "Keep the whole job amount. Only the monthly fee is charged.",
                        CommissionPercent = 0m,
                        SubscriptionFee = 1499m,
                        BillingPeriod = PlanBillingPeriod.Monthly,
                        DisplayOrder = 3,
                    });

                await context.SaveChangesAsync(ct);
                logger.LogInformation("Seeded 3 commission plans");
            }

            if (!await context.TaxRules.AnyAsync(ct))
            {
                var start = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

                context.TaxRules.AddRange(
                    new TaxRule
                    {
                        Name = "GST on platform commission",
                        Code = "GST_COMM",
                        CountryCode = TaxCountries.India,
                        Percent = 18m,
                        AppliesTo = TaxBase.Commission,
                        EffectiveFrom = start,
                        SortOrder = 1,
                        Note = "The platform charges the partner for a service, so GST applies to the commission, not the job.",
                    },
                    new TaxRule
                    {
                        Name = "GST on plan fee",
                        Code = "GST_PLAN",
                        CountryCode = TaxCountries.India,
                        Percent = 18m,
                        AppliesTo = TaxBase.SubscriptionFee,
                        EffectiveFrom = start,
                        SortOrder = 2,
                    },
                    new TaxRule
                    {
                        Name = "TDS under section 194-O",
                        Code = "TDS_194O",
                        CountryCode = TaxCountries.India,
                        Percent = 1m,
                        AppliesTo = TaxBase.GrossEarning,
                        ThresholdAmount = 0m,
                        EffectiveFrom = start,
                        SortOrder = 3,
                        Note = "Income tax withheld by the operator on the gross amount paid to the partner. Confirm the current rate and threshold before going live.",
                    });

                await context.SaveChangesAsync(ct);
                logger.LogInformation("Seeded 3 tax rules for India");
            }
        }

        private static async Task SeedMenuAsync(AppDbContext context, ILogger logger, CancellationToken ct)
        {
            if (await context.MenuItems.AnyAsync(ct))
            {
                await AddMissingMenuItemsAsync(context, logger, ct);
                return;
            }

            var dashboard = new MenuItem
            {
                Label = "Dashboard",
                ControllerName = "Admin",
                ActionName = "Index",
                Icon = "▦",
                SortOrder = 0,
            };

            var groups = new Dictionary<string, MenuItem>
            {
                ["Masters"] = new MenuItem { Label = "Masters", SortOrder = 10 },
                ["Approvals"] = new MenuItem { Label = "Approvals", SortOrder = 20 },
                ["User management"] = new MenuItem { Label = "User management", SortOrder = 30 },
                ["Account"] = new MenuItem { Label = "Account", SortOrder = 40 },
                ["Money"] = new MenuItem { Label = "Money", SortOrder = 45 },
                ["Operations"] = new MenuItem { Label = "Operations", SortOrder = 50 },
            };

            context.MenuItems.Add(dashboard);
            context.MenuItems.AddRange(groups.Values);
            await context.SaveChangesAsync(ct);

            var leaves = new[]
            {
                new MenuItem { Label = "Skill categories", ControllerName = "Admin", ActionName = "Categories", Icon = "☰", SortOrder = 1, ParentId = groups["Masters"].Id },
                new MenuItem { Label = "Services", ControllerName = "Admin", ActionName = "Services", Icon = "☰", SortOrder = 2, ParentId = groups["Masters"].Id },
                new MenuItem { Label = "Price insights", ControllerName = "Admin", ActionName = "Pricing", Icon = "₹", SortOrder = 3, ParentId = groups["Masters"].Id },
                new MenuItem { Label = "Commission plans", ControllerName = "Admin", ActionName = "Plans", Icon = "₹", SortOrder = 4, ParentId = groups["Masters"].Id },
                new MenuItem { Label = "Tax rules", ControllerName = "Admin", ActionName = "Taxes", Icon = "%", SortOrder = 5, ParentId = groups["Masters"].Id },
                new MenuItem { Label = "Menu", ControllerName = "Admin", ActionName = "Menus", Icon = "☰", SortOrder = 6, ParentId = groups["Masters"].Id, Visibility = MenuVisibility.SuperAdmin },
                new MenuItem { Label = "Partner KYC", ControllerName = "Admin", ActionName = "Approvals", Icon = "✓", SortOrder = 1, ParentId = groups["Approvals"].Id, BadgeKey = MenuBadgeKeys.PendingKyc },
                new MenuItem { Label = "Customers", ControllerName = "Admin", ActionName = "Customers", Icon = "◔", SortOrder = 1, ParentId = groups["User management"].Id },
                new MenuItem { Label = "Partners", ControllerName = "Admin", ActionName = "Partners", Icon = "◑", SortOrder = 2, ParentId = groups["User management"].Id },
                new MenuItem { Label = "Administrators", ControllerName = "Admin", ActionName = "Admins", Icon = "◕", SortOrder = 3, ParentId = groups["User management"].Id },
                new MenuItem { Label = "My profile", ControllerName = "Admin", ActionName = "Profile", Icon = "☻", SortOrder = 1, ParentId = groups["Account"].Id },
                new MenuItem { Label = "Tasks", ControllerName = "Admin", ActionName = "Tasks", Icon = "▤", SortOrder = 1, ParentId = groups["Operations"].Id },
                new MenuItem { Label = "Support enquiries", ControllerName = "Admin", ActionName = "Enquiries", Icon = "☎", SortOrder = 4, ParentId = groups["Operations"].Id, BadgeKey = MenuBadgeKeys.OpenEnquiries },
                new MenuItem { Label = "Storefront banners", ControllerName = "Admin", ActionName = "Banners", Icon = "▣", SortOrder = 7, ParentId = groups["Masters"].Id },
                new MenuItem { Label = "Service zones", ControllerName = "Admin", ActionName = "ServiceZones", Icon = "◎", SortOrder = 9, ParentId = groups["Masters"].Id },
                new MenuItem { Label = "Login settings", ControllerName = "Admin", ActionName = "AuthSettings", Icon = "⚿", SortOrder = 8, ParentId = groups["Masters"].Id, Visibility = MenuVisibility.SuperAdmin },
                new MenuItem { Label = "Partner payouts", ControllerName = "Admin", ActionName = "Payouts", Icon = "₹", SortOrder = 2, ParentId = groups["Money"].Id },
                new MenuItem { Label = "Error log", ControllerName = "Admin", ActionName = "Errors", Icon = "⚠", SortOrder = 3, ParentId = groups["Operations"].Id, Visibility = MenuVisibility.SuperAdmin },
                new MenuItem { Label = "API reference", Url = "/swagger", Icon = "↗", SortOrder = 2, ParentId = groups["Operations"].Id, OpensInNewTab = true },
            };

            context.MenuItems.AddRange(leaves);
            await context.SaveChangesAsync(ct);

            logger.LogInformation("Seeded {Count} admin menu items", leaves.Length + groups.Count + 1);
        }

        // The bulk seed runs only on an empty table, so a screen added later
        // would never appear on a database that already has a menu.
        private static async Task AddMissingMenuItemsAsync(
            AppDbContext context, ILogger logger, CancellationToken ct)
        {
            var wanted = new[]
            {
                (Group: "Operations", Item: new MenuItem
                {
                    Label = "Support enquiries",
                    ControllerName = "Admin",
                    ActionName = "Enquiries",
                    Icon = "☎",
                    SortOrder = 4,
                    BadgeKey = MenuBadgeKeys.OpenEnquiries,
                }),
                (Group: "Masters", Item: new MenuItem
                {
                    Label = "Storefront banners",
                    ControllerName = "Admin",
                    ActionName = "Banners",
                    Icon = "▣",
                    SortOrder = 7,
                }),
                (Group: "Masters", Item: new MenuItem
                {
                    Label = "Login settings",
                    ControllerName = "Admin",
                    ActionName = "AuthSettings",
                    Icon = "⚿",
                    SortOrder = 8,
                    Visibility = MenuVisibility.SuperAdmin,
                }),
                (Group: "Masters", Item: new MenuItem
                {
                    Label = "Service zones",
                    ControllerName = "Admin",
                    ActionName = "ServiceZones",
                    Icon = "◎",
                    SortOrder = 9,
                }),
            };

            var added = 0;

            foreach (var (groupLabel, item) in wanted)
            {
                var exists = await context.MenuItems.AnyAsync(
                    m => m.ControllerName == item.ControllerName
                      && m.ActionName == item.ActionName, ct);

                if (exists) continue;

                var group = await context.MenuItems.FirstOrDefaultAsync(
                    m => m.Label == groupLabel && m.ParentId == null, ct);

                if (group is null) continue;

                item.ParentId = group.Id;
                context.MenuItems.Add(item);
                added++;
            }

            if (added == 0) return;

            await context.SaveChangesAsync(ct);
            logger.LogInformation("Added {Count} missing admin menu items", added);
        }

        private static async Task SeedServiceItemsAsync(AppDbContext context, ILogger logger, CancellationToken ct)
        {
            var categories = await context.SkillCategories
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(ct);

            var existing = await context.ServiceItems
                .Select(s => new { s.SkillCategoryId, Name = s.Name.ToLower() })
                .ToListAsync(ct);

            var added = 0;

            foreach (var category in categories)
            {
                if (!DefaultServiceItems.TryGetValue(category.Name, out var names)) continue;

                var order = 0;

                foreach (var name in names)
                {
                    var alreadyThere = existing.Any(
                        e => e.SkillCategoryId == category.Id && e.Name == name.ToLowerInvariant());

                    if (!alreadyThere)
                    {
                        context.ServiceItems.Add(new ServiceItem
                        {
                            SkillCategoryId = category.Id,
                            Name = name,
                            IsActive = true,
                            DisplayOrder = order,
                            CreatedAt = DateTime.UtcNow,
                        });
                        added++;
                    }

                    order++;
                }
            }

            if (added > 0)
            {
                await context.SaveChangesAsync(ct);
                logger.LogInformation("Seeded {Count} service items", added);
            }
        }

        // A fresh database serves nothing until an admin draws a real zone —
        // this one just keeps local dev and the seeded demo accounts working
        // out of the box, centred on the seeded Gurugram addresses.
        private static async Task SeedServiceZonesAsync(AppDbContext context, ILogger logger, CancellationToken ct)
        {
            if (await context.ServiceZones.AnyAsync(ct)) return;

            var categoryIds = await context.SkillCategories.Select(c => c.Id).ToListAsync(ct);

            var zone = new ServiceZone
            {
                Name = "Gurugram",
                Center = GeoPoint.From(28.4949, 77.0885),
                RadiusKm = 25,
                IsActive = true,
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow,
                Categories = categoryIds.Select(id => new ServiceZoneCategory { SkillCategoryId = id }).ToList(),
            };

            context.ServiceZones.Add(zone);
            await context.SaveChangesAsync(ct);

            logger.LogInformation("Seeded default service zone {Name} with {Count} categories", zone.Name, categoryIds.Count);
        }

        private static async Task SeedCategoriesAsync(AppDbContext context, ILogger logger, CancellationToken ct)
        {
            var existing = await context.SkillCategories
                .Select(c => c.Name.ToLower())
                .ToListAsync(ct);

            var missing = DefaultCategories
                .Where(d => !existing.Contains(d.Name.ToLowerInvariant()))
                .ToList();

            if (missing.Count == 0) return;

            var nextOrder = await context.SkillCategories.AnyAsync(ct)
                ? await context.SkillCategories.MaxAsync(c => c.DisplayOrder, ct) + 1
                : 0;

            foreach (var (name, description) in missing)
            {
                context.SkillCategories.Add(new SkillCategory
                {
                    Name = name,
                    Description = description,
                    IsActive = true,
                    DisplayOrder = nextOrder++,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await context.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} skill categories", missing.Count);
        }

        private static async Task SeedAccountsAsync(AppDbContext context, ILogger logger, CancellationToken ct)
        {
            var changed = false;

            // There must always be exactly one way in to the super-admin-only
            // screens. If no super admin exists, promote the oldest admin —
            // otherwise a fresh database has nobody who can reset a password.
            if (!await context.Users.AnyAsync(u => u.Role == UserRoles.SuperAdmin, ct))
            {
                var founder = await context.Users
                    .Where(u => u.Role == UserRoles.Admin)
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync(ct);

                if (founder is not null)
                {
                    founder.Role = UserRoles.SuperAdmin;
                    changed = true;
                    logger.LogInformation("Promoted {Phone} to super admin", founder.Phone);
                }
            }

            // Accounts that predate authentication have no usable password.
            var passwordless = await context.Users
                .Where(u => u.PasswordHash == "")
                .ToListAsync(ct);

            foreach (var user in passwordless)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DevPassword);

                // No OTP flow exists yet; pre-verify so seeded accounts stay
                // usable once verification starts being enforced.
                user.IsPhoneVerified = true;
                user.PhoneVerifiedAt = DateTime.UtcNow;

                changed = true;
                logger.LogInformation("Seeded dev password for {Phone}", user.Phone);
            }

            // A user with role 'partner' is not usable without a profile row.
            var partnersMissingProfile = await context.Users
                .Where(u => u.Role == UserRoles.Partner
                            && !context.Partners.Any(p => p.UserId == u.Id))
                .ToListAsync(ct);

            if (partnersMissingProfile.Count > 0)
            {
                var fallbackCategoryId = await context.SkillCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(ct);

                if (fallbackCategoryId == 0)
                {
                    logger.LogWarning("Cannot create partner profiles — no active skill category exists.");
                }
                else
                {
                    foreach (var user in partnersMissingProfile)
                    {
                        context.Partners.Add(new Partner
                        {
                            UserId = user.Id,
                            SkillCategoryId = fallbackCategoryId,
                            KycStatus = Models.KycStatus.Approved,   // pre-approved so the dev flow is usable end to end
                            IsAvailable = true,
                            CreatedAt = DateTime.UtcNow,
                        });
                        changed = true;
                        logger.LogInformation("Created partner profile for {Phone}", user.Phone);
                    }
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync(ct);
                logger.LogInformation("Dev seeding complete. Seeded accounts use password: {Password}", DevPassword);
            }
        }
    }
}
