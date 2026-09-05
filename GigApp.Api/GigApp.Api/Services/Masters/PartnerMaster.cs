using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Masters
{
    /// <summary>
    /// Partners who may actually be given work — approved KYC, active account,
    /// and in the category the parent id names. Support assigns from this list,
    /// so anyone missing from it is someone the rules already refuse.
    /// </summary>
    public class PartnerMasterSource : IMasterSource
    {
        private readonly AppDbContext _context;

        public PartnerMasterSource(AppDbContext context) => _context = context;

        public string Key => "assignable-partner";

        public string[]? Roles => new[] { UserRoles.Admin, UserRoles.SuperAdmin };

        public async Task<IReadOnlyList<MasterItemDto>> SearchAsync(
            string? term, int? parentId, int limit, CancellationToken ct)
        {
            if (parentId is null or <= 0) return Array.Empty<MasterItemDto>();

            var query = _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.SkillCategoryId == parentId
                         && p.KycStatus == Models.KycStatus.Approved
                         && p.User!.IsActive);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var search = term.Trim().ToLower();
                query = query.Where(p =>
                    p.User!.Name.ToLower().Contains(search) || p.User.Phone.Contains(search));
            }

            return await query
                .OrderBy(p => p.User!.Name)
                .Take(limit)
                .Select(p => new MasterItemDto
                {
                    Id = p.Id,
                    Name = p.User!.Name,
                    Hint = p.User.Phone,
                })
                .ToListAsync(ct);
        }
    }
}
