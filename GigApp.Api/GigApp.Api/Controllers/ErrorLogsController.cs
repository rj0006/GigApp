using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.SuperAdminOnly)]
    public class ErrorLogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ErrorLogsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ErrorLogDto>>> GetAllPaged(
            [FromQuery] PageRequest paging, bool showResolved, CancellationToken ct)
        {
            var query = _context.ErrorLogs.AsNoTracking();
            if (!showResolved) query = query.Where(l => !l.IsResolved);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(l => l.Reference.ToLower().Contains(term)
                                      || l.Message.ToLower().Contains(term)
                                      || (l.Module != null && l.Module.ToLower().Contains(term)));
            }

            var page = await query.OrderByDescending(l => l.OccurredAt).ToPagedResultAsync(paging, ct);

            return Ok(page.Map(ErrorLogDto.From));
        }

        [HttpPost("{id:long}/resolve")]
        [TrackForm("ErrorLog")]
        public async Task<ActionResult<ErrorLogDto>> Resolve(
            long id, ResolveErrorLogRequest request, CancellationToken ct)
        {
            var log = await _context.ErrorLogs.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (log is null) return NotFound();

            log.IsResolved = true;
            log.ResolutionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            log.ResolvedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return Ok(ErrorLogDto.From(log));
        }
    }
}
