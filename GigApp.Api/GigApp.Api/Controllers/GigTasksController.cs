using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GigTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<GigTasksController> _logger;

        public GigTasksController(AppDbContext context, ILogger<GigTasksController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: api/gigtasks  -> Customer posts a new task
        [HttpPost]
        [Authorize(Policy = Policies.CustomerOnly)]
        public async Task<ActionResult<GigTaskDto>> CreateTask(
            CreateGigTaskRequest request, CancellationToken ct)
        {
            var categoryIsSelectable = await _context.SkillCategories
                .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct);

            if (!categoryIsSelectable)
                return BadRequest(new ProblemDetails { Title = "Choose a valid category.", Status = 400 });

            // CustomerId comes from the token, never the request body.
            var task = new GigTask
            {
                CustomerId = User.GetRequiredUserId(),
                CategoryId = request.CategoryId,
                Description = request.Description.Trim(),
                Address = request.Address.Trim(),
                Budget = request.Budget,
                PreferredDateTime = request.PreferredDateTime.ToUtc(),
                Status = GigTaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };

            _context.GigTasks.Add(task);
            await _context.SaveChangesAsync(ct);

            // Reload so the response carries the category and customer names,
            // matching what every other endpoint returns.
            return CreatedAtAction(
                nameof(GetTaskById),
                new { id = task.Id },
                GigTaskDto.From(await LoadDetailedAsync(task.Id, ct)));
        }

        // GET: api/gigtasks/available  -> Verified partners browse open work
        [HttpGet("available")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<IEnumerable<GigTaskDto>>> GetAvailableTasks(
            [FromQuery] int? categoryId, CancellationToken ct)
        {
            var partner = await GetCurrentPartnerAsync(ct);
            if (partner is null) return Forbid();

            if (!partner.IsVerified)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ProblemDetails { Title = "Your account is pending KYC verification.", Status = 403 });

            var query = _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Customer)
                .Include(t => t.Category)
                .Where(t => t.Status == GigTaskStatus.Pending);

            if (categoryId is not null)
                query = query.Where(t => t.CategoryId == categoryId);

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            return Ok(tasks.Select(GigTaskDto.From));
        }

        // GET: api/gigtasks/my  -> Caller's own tasks, whichever side they are on
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<GigTaskDto>>> GetMyTasks(CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();

            var query = DetailedTasks;

            if (User.IsInRole(UserRoles.Partner))
            {
                var partner = await GetCurrentPartnerAsync(ct);
                if (partner is null) return Forbid();
                query = query.Where(t => t.PartnerId == partner.Id);
            }
            else
            {
                query = query.Where(t => t.CustomerId == userId);
            }

            var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
            return Ok(tasks.Select(GigTaskDto.From));
        }

        // GET: api/gigtasks/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<GigTaskDto>> GetTaskById(int id, CancellationToken ct)
        {
            var task = await DetailedTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

            if (task is null) return NotFound();

            if (!await CanViewAsync(task, ct)) return Forbid();

            return Ok(GigTaskDto.From(task));
        }

        // GET: api/gigtasks/customer/3  -> Admin lookup of any customer's history
        [HttpGet("customer/{customerId:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<IEnumerable<GigTaskDto>>> GetTasksByCustomer(
            int customerId, CancellationToken ct)
        {
            var tasks = await DetailedTasks
                .Where(t => t.CustomerId == customerId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            return Ok(tasks.Select(GigTaskDto.From));
        }

        // PUT: api/gigtasks/5/accept  -> Partner claims a task
        [HttpPut("{id:int}/accept")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<GigTaskDto>> AcceptTask(int id, CancellationToken ct)
        {
            var partner = await GetCurrentPartnerAsync(ct);
            if (partner is null) return Forbid();

            if (!partner.IsVerified)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ProblemDetails { Title = "Your account is pending KYC verification.", Status = 403 });

            // Single conditional UPDATE — the WHERE clause is the lock. Two
            // partners racing here cannot both come back with a row count of 1.
            var rowsAffected = await _context.GigTasks
                .Where(t => t.Id == id && t.Status == GigTaskStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.PartnerId, partner.Id)
                    .SetProperty(t => t.Status, GigTaskStatus.Accepted), ct);

            if (rowsAffected == 0)
            {
                var exists = await _context.GigTasks.AnyAsync(t => t.Id == id, ct);
                return exists
                    ? Conflict(new ProblemDetails { Title = "This task is no longer available.", Status = 409 })
                    : NotFound();
            }

            _logger.LogInformation("Task {TaskId} accepted by partner {PartnerId}", id, partner.Id);

            return Ok(GigTaskDto.From(await LoadDetailedAsync(id, ct)));
        }

        // PUT: api/gigtasks/5/status  -> Move a task along its lifecycle
        [HttpPut("{id:int}/status")]
        public async Task<ActionResult<GigTaskDto>> UpdateStatus(
            int id, UpdateGigTaskStatusRequest request, CancellationToken ct)
        {
            var newStatus = request.Status?.Trim().ToLowerInvariant() ?? string.Empty;

            if (!GigTaskStatus.IsValid(newStatus))
                return BadRequest(new ProblemDetails { Title = $"Unknown status '{request.Status}'.", Status = 400 });

            var task = await _context.GigTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (task is null) return NotFound();

            var permitted = await GetPermittedTransitionAsync(task, newStatus, ct);
            if (permitted is not null) return permitted;

            if (!GigTaskStatus.CanTransition(task.Status, newStatus))
                return Conflict(new ProblemDetails
                {
                    Title = $"Cannot move a task from '{task.Status}' to '{newStatus}'.",
                    Status = 409,
                });

            task.Status = newStatus;
            task.CompletedAt = newStatus == GigTaskStatus.Completed ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Task {TaskId} moved to {Status}", id, newStatus);

            return Ok(GigTaskDto.From(await LoadDetailedAsync(id, ct)));
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        /// Tasks with customer and partner names attached — the shape every
        /// endpoint returns, so responses stay consistent.
        /// </summary>
        private IQueryable<GigTask> DetailedTasks =>
            _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.Customer)
                .Include(t => t.Partner)!.ThenInclude(p => p!.User);

        private Task<GigTask> LoadDetailedAsync(int id, CancellationToken ct) =>
            DetailedTasks.FirstAsync(t => t.Id == id, ct);

        private async Task<Partner?> GetCurrentPartnerAsync(CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();
            return await _context.Partners.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        }

        private async Task<bool> CanViewAsync(GigTask task, CancellationToken ct)
        {
            if (User.IsAdmin()) return true;

            var userId = User.GetRequiredUserId();
            if (task.CustomerId == userId) return true;

            if (task.PartnerId is null) return false;

            var partner = await GetCurrentPartnerAsync(ct);
            return partner is not null && task.PartnerId == partner.Id;
        }

        /// <summary>
        /// Returns a failure result when the caller may not make this particular
        /// transition, or null when they may. Cancellation is open to both sides;
        /// progress and completion belong to the assigned partner.
        /// </summary>
        private async Task<ActionResult?> GetPermittedTransitionAsync(
            GigTask task, string newStatus, CancellationToken ct)
        {
            if (User.IsAdmin()) return null;

            var userId = User.GetRequiredUserId();
            var isCustomer = task.CustomerId == userId;

            var partner = User.IsInRole(UserRoles.Partner) ? await GetCurrentPartnerAsync(ct) : null;
            var isAssignedPartner = partner is not null && task.PartnerId == partner.Id;

            if (!isCustomer && !isAssignedPartner) return Forbid();

            if (newStatus == GigTaskStatus.Cancelled) return null;

            // in_progress and completed are the partner's to report.
            return isAssignedPartner
                ? null
                : StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = $"Only the assigned partner can set a task to '{newStatus}'.",
                    Status = 403,
                });
        }
    }
}
