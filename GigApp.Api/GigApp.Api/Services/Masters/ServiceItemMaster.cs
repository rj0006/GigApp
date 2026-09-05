using GigApp.Api.Data;
using GigApp.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Masters
{
    /// <summary>
    /// Service items master. Depends on a category, so it needs the parent id —
    /// asking for every service item across every category would give the user
    /// an unusable list.
    /// </summary>
    public class ServiceItemMasterSource : IMasterSource
    {
        private readonly AppDbContext _context;

        public ServiceItemMasterSource(AppDbContext context) => _context = context;

        public string Key => "service-item";

        public async Task<IReadOnlyList<MasterItemDto>> SearchAsync(
            string? term, int? parentId, int limit, CancellationToken ct)
        {
            // Without a category there is nothing sensible to offer.
            if (parentId is null or <= 0) return Array.Empty<MasterItemDto>();

            var query = _context.ServiceItems
                .AsNoTracking()
                .Where(s => s.IsActive && s.SkillCategoryId == parentId);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var search = term.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(search));
            }

            var items = await query
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .Take(limit)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.BasePayout,
                    s.AllowsInstantBooking,
                })
                .ToListAsync(ct);

            return items.Select(s =>
            {
                var isInstant = s.AllowsInstantBooking && s.BasePayout is > 0;

                return new MasterItemDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Hint = isInstant
                        ? $"Fixed price ₹{s.BasePayout!.Value:N0}"
                        : s.Description,
                    Extra = isInstant
                        ? new Dictionary<string, string>
                        {
                            ["fixedPrice"] = s.BasePayout!.Value.ToString("0.##"),
                        }
                        : null,
                };
            }).ToList();
        }
    }
}
