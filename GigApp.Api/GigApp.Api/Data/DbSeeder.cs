using GigApp.Api.Models;
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
                            IsVerified = true,     // pre-approved so the dev flow is usable end to end
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
