using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Support
{
    public record EnquiryResult(bool Succeeded, string? Error, SupportEnquiryDto? Enquiry)
    {
        public static EnquiryResult Ok(SupportEnquiryDto enquiry) => new(true, null, enquiry);
        public static EnquiryResult Fail(string error) => new(false, error, null);
    }

    /// <summary>
    /// Support enquiries raised against one task. Only the two people on that
    /// task may raise one, and only an administrator may close it.
    /// </summary>
    public interface ISupportService
    {
        Task<EnquiryResult> RaiseAsync(
            int userId, string role, int taskId, RaiseEnquiryRequest request,
            CancellationToken ct = default);

        Task<EnquiryResult> ReviewAsync(
            int adminUserId, int enquiryId, ResolveEnquiryRequest request,
            CancellationToken ct = default);

        Task<IReadOnlyDictionary<int, SupportEnquiryDto>> LatestForTasksAsync(
            int userId, IEnumerable<int> taskIds, CancellationToken ct = default);

        Task<PagedResult<SupportEnquiryDto>> ListAsync(
            PageRequest paging, string? status, CancellationToken ct = default);

        Task<int> OpenCountAsync(CancellationToken ct = default);

        public const string RaisedMessage =
            "We have noted your complaint. Our team will connect with you shortly.";
    }

    public class SupportService : ISupportService
    {
        private readonly AppDbContext _context;

        public SupportService(AppDbContext context) => _context = context;

        public async Task<EnquiryResult> RaiseAsync(
            int userId, string role, int taskId, RaiseEnquiryRequest request,
            CancellationToken ct = default)
        {
            if (!EnquiryTopics.IsValid(request.Topic))
                return EnquiryResult.Fail("Choose what the problem is about.");

            var message = request.Message?.Trim() ?? string.Empty;
            if (message.Length < 10)
                return EnquiryResult.Fail("Describe the problem in at least ten characters.");

            var task = await _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Partner)
                .FirstOrDefaultAsync(t => t.Id == taskId, ct);

            if (task is null) return EnquiryResult.Fail("Task not found.");

            var isTheirs = role == UserRoles.Partner
                ? task.Partner?.UserId == userId
                : task.CustomerId == userId;

            if (!isTheirs) return EnquiryResult.Fail("This is not your order.");

            var alreadyOpen = await _context.SupportEnquiries.AnyAsync(
                e => e.GigTaskId == taskId
                  && e.RaisedByUserId == userId
                  && EnquiryStatus.Live.Contains(e.Status), ct);

            if (alreadyOpen)
                return EnquiryResult.Fail(
                    "You already have an open enquiry on this order. We will reply on that one.");

            var enquiry = new SupportEnquiry
            {
                GigTaskId = taskId,
                RaisedByUserId = userId,
                RaisedByRole = role,
                Topic = request.Topic,
                Message = message,
                Status = EnquiryStatus.Open,
                CreatedAt = DateTime.UtcNow,
            };

            _context.SupportEnquiries.Add(enquiry);
            await _context.SaveChangesAsync(ct);

            return EnquiryResult.Ok(SupportEnquiryDto.From(enquiry));
        }

        public async Task<EnquiryResult> ReviewAsync(
            int adminUserId, int enquiryId, ResolveEnquiryRequest request,
            CancellationToken ct = default)
        {
            if (!EnquiryStatus.IsValid(request.Status))
                return EnquiryResult.Fail("Choose a valid status.");

            var resolution = request.Resolution?.Trim();

            if (request.Status == EnquiryStatus.Resolved && string.IsNullOrWhiteSpace(resolution))
                return EnquiryResult.Fail(
                    "Write what was done. It is the only thing the person who raised this will see.");

            var enquiry = await _context.SupportEnquiries
                .FirstOrDefaultAsync(e => e.Id == enquiryId, ct);

            if (enquiry is null) return EnquiryResult.Fail("Enquiry not found.");

            if (enquiry.Status == EnquiryStatus.Resolved)
                return EnquiryResult.Fail("This enquiry has already been resolved.");

            enquiry.Status = request.Status;
            enquiry.UpdatedAt = DateTime.UtcNow;

            if (request.Status == EnquiryStatus.Resolved)
            {
                enquiry.Resolution = resolution;
                enquiry.ResolvedByUserId = adminUserId;
                enquiry.ResolvedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);

            return EnquiryResult.Ok(SupportEnquiryDto.From(enquiry));
        }

        public async Task<IReadOnlyDictionary<int, SupportEnquiryDto>> LatestForTasksAsync(
            int userId, IEnumerable<int> taskIds, CancellationToken ct = default)
        {
            var ids = taskIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, SupportEnquiryDto>();

            var enquiries = await _context.SupportEnquiries
                .AsNoTracking()
                .Include(e => e.ResolvedByUser)
                .Where(e => ids.Contains(e.GigTaskId) && e.RaisedByUserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(ct);

            return enquiries
                .GroupBy(e => e.GigTaskId)
                .ToDictionary(g => g.Key, g => SupportEnquiryDto.From(g.First()));
        }

        public async Task<PagedResult<SupportEnquiryDto>> ListAsync(
            PageRequest paging, string? status, CancellationToken ct = default)
        {
            var query = _context.SupportEnquiries
                .AsNoTracking()
                .Include(e => e.RaisedByUser)
                .Include(e => e.ResolvedByUser)
                .Include(e => e.GigTask)!.ThenInclude(t => t!.Category)
                .AsQueryable();

            if (EnquiryStatus.IsValid(status)) query = query.Where(e => e.Status == status);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(e =>
                    e.Message.ToLower().Contains(term)
                    || e.RaisedByUser!.Name.ToLower().Contains(term)
                    || e.RaisedByUser.Phone.Contains(term));
            }

            // Live enquiries first, then newest — a resolved one is not waiting
            // on anybody and should not push open work down the page.
            var page = await query
                .OrderBy(e => e.Status == EnquiryStatus.Open ? 0
                            : e.Status == EnquiryStatus.InProgress ? 1 : 2)
                .ThenByDescending(e => e.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return page.Map(SupportEnquiryDto.From);
        }

        public Task<int> OpenCountAsync(CancellationToken ct = default) =>
            _context.SupportEnquiries.CountAsync(e => EnquiryStatus.Live.Contains(e.Status), ct);
    }
}
