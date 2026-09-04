using GigApp.Api.Data;
using GigApp.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services
{
    /// <summary>
    /// Single source for category pickers. Everything that offers a category
    /// choice goes through here, so an inactive category can never be selected
    /// from anywhere.
    /// </summary>
    public interface ICategoryLookup
    {
        Task<IReadOnlyList<SkillCategoryOptionDto>> GetActiveOptionsAsync(CancellationToken ct = default);

        /// <summary>
        /// Options for an edit form: active categories plus the one currently
        /// selected, so a partner already on a deactivated category still sees it.
        /// </summary>
        Task<IReadOnlyList<SkillCategoryOptionDto>> GetOptionsIncludingAsync(
            int? selectedId, CancellationToken ct = default);

        Task<bool> IsSelectableAsync(int categoryId, CancellationToken ct = default);

        Task<string?> GetNameAsync(int categoryId, CancellationToken ct = default);
    }

    public class CategoryLookup : ICategoryLookup
    {
        private readonly AppDbContext _context;

        public CategoryLookup(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<SkillCategoryOptionDto>> GetActiveOptionsAsync(CancellationToken ct = default) =>
            await Ordered(_context.SkillCategories.Where(c => c.IsActive)).ToListAsync(ct);

        public async Task<IReadOnlyList<SkillCategoryOptionDto>> GetOptionsIncludingAsync(
            int? selectedId, CancellationToken ct = default) =>
            await Ordered(_context.SkillCategories.Where(c => c.IsActive || c.Id == selectedId)).ToListAsync(ct);

        public Task<bool> IsSelectableAsync(int categoryId, CancellationToken ct = default) =>
            _context.SkillCategories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct);

        public Task<string?> GetNameAsync(int categoryId, CancellationToken ct = default) =>
            _context.SkillCategories.AsNoTracking()
                .Where(c => c.Id == categoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct)!;

        private static IQueryable<SkillCategoryOptionDto> Ordered(IQueryable<Models.SkillCategory> query) =>
            query.AsNoTracking()
                 .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                 .Select(c => new SkillCategoryOptionDto { Id = c.Id, Name = c.Name });
    }
}
