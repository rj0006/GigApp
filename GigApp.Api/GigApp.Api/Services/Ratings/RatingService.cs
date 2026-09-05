using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Ratings
{
    public record RatingResult(bool Succeeded, string? Error)
    {
        public static RatingResult Ok() => new(true, null);
        public static RatingResult Fail(string error) => new(false, error);
    }

    /// <summary>
    /// Both sides of a finished job rate each other. The partner's rating is
    /// taken while they are still in the app closing the job, so it is required
    /// there; the customer may never come back, so theirs is optional and can be
    /// left until later.
    /// </summary>
    public interface IRatingService
    {
        Task<RatingResult> RateAsync(
            int raterUserId, string raterRole, int taskId,
            int stars, string? feedback, CancellationToken ct = default);

        Task<TaskRating?> BuildAsync(
            int raterUserId, string raterRole, GigTask task,
            int stars, string? feedback, CancellationToken ct = default);

        Task RefreshAverageAsync(int ratedUserId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<int, TaskRatingDto>> ForTasksAsync(
            IEnumerable<int> taskIds, string raterRole, CancellationToken ct = default);
    }

    public class RatingService : IRatingService
    {
        private readonly AppDbContext _context;

        public RatingService(AppDbContext context) => _context = context;

        public async Task<RatingResult> RateAsync(
            int raterUserId, string raterRole, int taskId,
            int stars, string? feedback, CancellationToken ct = default)
        {
            var task = await _context.GigTasks
                .Include(t => t.Partner)
                .FirstOrDefaultAsync(t => t.Id == taskId, ct);

            if (task is null) return RatingResult.Fail("Task not found.");

            var rating = await BuildAsync(raterUserId, raterRole, task, stars, feedback, ct);
            if (rating is null)
                return RatingResult.Fail("This job cannot be rated by you, or you have already rated it.");

            _context.TaskRatings.Add(rating);
            await _context.SaveChangesAsync(ct);

            await RefreshAverageAsync(RatedUserId(task, raterRole), ct);

            return RatingResult.Ok();
        }

        public async Task<TaskRating?> BuildAsync(
            int raterUserId, string raterRole, GigTask task,
            int stars, string? feedback, CancellationToken ct = default)
        {
            if (!RatedBy.IsValid(raterRole) || !RatingScale.IsValid(stars)) return null;

            var isTheirs = raterRole == RatedBy.Customer
                ? task.CustomerId == raterUserId
                : task.Partner?.UserId == raterUserId;

            if (!isTheirs) return null;

            // The partner rates while they are closing the job, so the status is
            // still moving. Everyone else has to wait until it has finished.
            if (raterRole == RatedBy.Customer && task.Status != GigTaskStatus.Completed) return null;

            var already = await _context.TaskRatings
                .AnyAsync(r => r.GigTaskId == task.Id && r.RaterRole == raterRole, ct);

            if (already) return null;

            return new TaskRating
            {
                GigTaskId = task.Id,
                RaterUserId = raterUserId,
                RaterRole = raterRole,
                Stars = stars,
                Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
        }

        public async Task RefreshAverageAsync(int ratedUserId, CancellationToken ct = default)
        {
            if (ratedUserId <= 0) return;

            var received = await _context.TaskRatings
                .Where(r => r.RaterRole == RatedBy.Customer
                    ? r.GigTask!.Partner!.UserId == ratedUserId
                    : r.GigTask!.CustomerId == ratedUserId)
                .GroupBy(r => 1)
                .Select(g => new { Count = g.Count(), Average = g.Average(r => (decimal)r.Stars) })
                .FirstOrDefaultAsync(ct);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == ratedUserId, ct);
            if (user is null) return;

            user.RatingCount = received?.Count ?? 0;
            user.AverageRating = received is null ? null : Math.Round(received.Average, 2);

            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyDictionary<int, TaskRatingDto>> ForTasksAsync(
            IEnumerable<int> taskIds, string raterRole, CancellationToken ct = default)
        {
            var ids = taskIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, TaskRatingDto>();

            return await _context.TaskRatings
                .AsNoTracking()
                .Where(r => ids.Contains(r.GigTaskId) && r.RaterRole == raterRole)
                .ToDictionaryAsync(r => r.GigTaskId, TaskRatingDto.From, ct);
        }

        private static int RatedUserId(GigTask task, string raterRole) =>
            raterRole == RatedBy.Customer
                ? task.Partner?.UserId ?? 0
                : task.CustomerId;
    }
}
