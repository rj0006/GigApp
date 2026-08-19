using GigApp.Api.Data;
using GigApp.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Masters
{
    /// <summary>Skill categories master. Only active rows are ever offered.</summary>
    public class SkillCategoryMasterSource : IMasterSource
    {
        private readonly AppDbContext _context;

        public SkillCategoryMasterSource(AppDbContext context) => _context = context;

        public string Key => "skill-category";

        public async Task<IReadOnlyList<MasterItemDto>> SearchAsync(
            string? term, int limit, CancellationToken ct)
        {
            var query = _context.SkillCategories.AsNoTracking().Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var search = term.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(search));
            }

            return await query
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Take(limit)
                .Select(c => new MasterItemDto { Id = c.Id, Name = c.Name, Hint = c.Description })
                .ToListAsync(ct);
        }
    }
}
