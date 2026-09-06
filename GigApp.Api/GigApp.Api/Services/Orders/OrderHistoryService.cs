using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Orders
{
    /// <summary>
    /// Every order a person has been on, whichever side they were. The customer
    /// and partner portals both render the same list, so the role decides the
    /// filter and nothing else.
    /// </summary>
    public interface IOrderHistoryService
    {
        Task<PagedResult<GigTaskDto>> ForUserAsync(
            int userId, string role, PageRequest paging, string? status,
            CancellationToken ct = default);
    }

    public class OrderHistoryService : IOrderHistoryService
    {
        private readonly AppDbContext _context;

        public OrderHistoryService(AppDbContext context) => _context = context;

        public async Task<PagedResult<GigTaskDto>> ForUserAsync(
            int userId, string role, PageRequest paging, string? status,
            CancellationToken ct = default)
        {
            var query = _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.ServiceItem)
                .Include(t => t.Customer)
                .Include(t => t.AssignedBy)
                .Include(t => t.Partner)!.ThenInclude(p => p!.User)
                .AsQueryable();

            query = role == UserRoles.Partner
                ? query.Where(t => t.Partner!.UserId == userId)
                : query.Where(t => t.CustomerId == userId);

            if (GigTaskStatus.IsValid(status)) query = query.Where(t => t.Status == status);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(t =>
                    t.Description.ToLower().Contains(term)
                    || t.Address.ToLower().Contains(term)
                    || t.Category!.Name.ToLower().Contains(term));
            }

            var page = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return page.Map(GigTaskDto.From);
        }
    }
}
