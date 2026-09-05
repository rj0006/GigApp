using GigApp.Api.Data;
using GigApp.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services
{
    /// <summary>
    /// Single place that decides whether a service item may be chosen. Both the
    /// API and the portals go through it, so a client cannot post a service item
    /// belonging to a different category than the one it claims.
    /// </summary>
    public interface IServiceItemLookup
    {
        Task<BookableServiceItem?> GetBookableAsync(
            int serviceItemId, int categoryId, CancellationToken ct = default);

        Task<IReadOnlyList<SkillCategoryOptionDto>> GetOptionsAsync(
            int categoryId, CancellationToken ct = default);
    }

    public record BookableServiceItem(
        int Id, string Name, decimal? BasePayout, bool AllowsInstantBooking)
    {
        public bool IsInstant => AllowsInstantBooking && BasePayout is > 0;

        public decimal FixedPrice => BasePayout!.Value;
    }

    public class ServiceItemLookup : IServiceItemLookup
    {
        private readonly AppDbContext _context;

        public ServiceItemLookup(AppDbContext context) => _context = context;

        public Task<BookableServiceItem?> GetBookableAsync(
            int serviceItemId, int categoryId, CancellationToken ct = default) =>
            _context.ServiceItems
                .AsNoTracking()
                .Where(s => s.Id == serviceItemId && s.SkillCategoryId == categoryId && s.IsActive)
                .Select(s => new BookableServiceItem(
                    s.Id, s.Name, s.BasePayout, s.AllowsInstantBooking))
                .FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<SkillCategoryOptionDto>> GetOptionsAsync(
            int categoryId, CancellationToken ct = default) =>
            await _context.ServiceItems
                .AsNoTracking()
                .Where(s => s.SkillCategoryId == categoryId && s.IsActive)
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .Select(s => new SkillCategoryOptionDto { Id = s.Id, Name = s.Name })
                .ToListAsync(ct);
    }
}
