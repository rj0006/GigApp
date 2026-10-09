using GigApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services
{
    public static class JoinRowExtensions
    {
        public static void SyncIds<T>(
            this ICollection<T> rows, IEnumerable<int> wanted, Func<T, int> idOf, Func<int, T> create)
        {
            var target = wanted.Distinct().ToHashSet();

            foreach (var stale in rows.Where(row => !target.Contains(idOf(row))).ToList())
                rows.Remove(stale);

            var present = rows.Select(idOf).ToHashSet();

            foreach (var id in target.Where(id => !present.Contains(id)))
                rows.Add(create(id));
        }

        public static Task<List<int>> ExistingZoneIdsAsync(
            this AppDbContext context, IEnumerable<int> ids, CancellationToken ct)
        {
            var wanted = ids.Distinct().ToList();

            return context.ServiceZones
                .Where(zone => wanted.Contains(zone.Id))
                .Select(zone => zone.Id)
                .ToListAsync(ct);
        }
    }
}
