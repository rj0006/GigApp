using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Geo
{
    /// <summary>
    /// Ranks partners for a task, and tasks for a partner, by how far apart they
    /// are. Distance is measured by PostGIS on a geography column, so it is real
    /// metres over the earth rather than a flat approximation, and the GIST index
    /// does the filtering rather than the application.
    /// </summary>
    public interface IMatchService
    {
        Task<IReadOnlyList<PartnerMatchDto>> RankPartnersAsync(
            int taskId, int limit = 20, CancellationToken ct = default);

        Task<IReadOnlyDictionary<int, double>> DistancesFromPartnerAsync(
            int partnerId, IEnumerable<int> taskIds, CancellationToken ct = default);
    }

    public class MatchService : IMatchService
    {
        private readonly AppDbContext _context;

        public MatchService(AppDbContext context) => _context = context;

        public async Task<IReadOnlyList<PartnerMatchDto>> RankPartnersAsync(
            int taskId, int limit = 20, CancellationToken ct = default)
        {
            var task = await _context.GigTasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new { t.CategoryId, t.Location })
                .FirstOrDefaultAsync(ct);

            if (task is null) return Array.Empty<PartnerMatchDto>();

            var candidates = _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.SkillCategoryId == task.CategoryId
                         && p.KycStatus == KycStatus.Approved
                         && p.User!.IsActive);

            var rows = await candidates
                .Select(p => new
                {
                    p.Id,
                    Name = p.User!.Name,
                    Phone = p.User.Phone,
                    p.User.AverageRating,
                    p.User.RatingCount,
                    p.IsAvailable,
                    p.ServiceRadiusKm,
                    Metres = task.Location == null || p.BaseLocation == null
                        ? (double?)null
                        : p.BaseLocation.Distance(task.Location),
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new PartnerMatchDto
                {
                    PartnerId = r.Id,
                    Name = r.Name,
                    Phone = r.Phone,
                    AverageRating = r.AverageRating,
                    RatingCount = r.RatingCount,
                    IsAvailable = r.IsAvailable,
                    ServiceRadiusKm = r.ServiceRadiusKm,
                    DistanceKm = r.Metres is null ? null : GeoPoint.KmFromMetres(r.Metres.Value),
                })
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.DistanceKm ?? double.MaxValue)
                .Take(limit)
                .ToList();
        }

        public async Task<IReadOnlyDictionary<int, double>> DistancesFromPartnerAsync(
            int partnerId, IEnumerable<int> taskIds, CancellationToken ct = default)
        {
            var ids = taskIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, double>();

            var origin = await _context.Partners
                .AsNoTracking()
                .Where(p => p.Id == partnerId)
                .Select(p => p.BaseLocation)
                .FirstOrDefaultAsync(ct);

            if (origin is null) return new Dictionary<int, double>();

            var rows = await _context.GigTasks
                .AsNoTracking()
                .Where(t => ids.Contains(t.Id) && t.Location != null)
                .Select(t => new { t.Id, Metres = t.Location!.Distance(origin) })
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.Id, r => GeoPoint.KmFromMetres(r.Metres));
        }
    }
}
