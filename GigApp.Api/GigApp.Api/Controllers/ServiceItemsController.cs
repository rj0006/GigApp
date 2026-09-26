using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ServiceItemsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;
        private readonly IFileStorageService _storage;
        private readonly ILogger<ServiceItemsController> _logger;

        public ServiceItemsController(
            AppDbContext context, ICategoryLookup categories, IFileStorageService storage,
            ILogger<ServiceItemsController> logger)
        {
            _context = context;
            _categories = categories;
            _storage = storage;
            _logger = logger;
        }

        // GET: api/serviceitems/bookable?categoryId=5
        // Any signed-in caller (a customer picking what to book) — always active-only,
        // never trusts a showInactive flag the way the admin list below does, same
        // reasoning as SkillCategoriesController.GetActive being anonymous-safe.
        [HttpGet("bookable")]
        [Authorize]
        public async Task<ActionResult<IReadOnlyList<ServiceItemDto>>> GetBookable(
            int categoryId, CancellationToken ct)
        {
            var items = await _context.ServiceItems
                .AsNoTracking()
                .Include(s => s.SkillCategory)
                .Where(s => s.SkillCategoryId == categoryId && s.IsActive)
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .Select(s => ServiceItemDto.From(s))
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET: api/serviceitems?page=1&pageSize=10&search=&categoryId=&showInactive=true
        [HttpGet]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<PagedResult<ServiceItemDto>>> GetAll(
            [FromQuery] PageRequest paging, int? categoryId, bool showInactive, CancellationToken ct)
        {
            var query = _context.ServiceItems.AsNoTracking().Include(s => s.SkillCategory).AsQueryable();

            if (!showInactive) query = query.Where(s => s.IsActive);
            if (categoryId is not null) query = query.Where(s => s.SkillCategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term));
            }

            var page = await query
                .OrderBy(s => s.SkillCategory!.DisplayOrder).ThenBy(s => s.SkillCategory!.Name)
                .ThenBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                .Select(s => new ServiceItemDto
                {
                    Id = s.Id,
                    SkillCategoryId = s.SkillCategoryId,
                    CategoryName = s.SkillCategory!.Name,
                    Name = s.Name,
                    Description = s.Description,
                    ImageFileName = s.ImageFileName,
                    BasePayout = s.BasePayout,
                    AllowsInstantBooking = s.AllowsInstantBooking,
                    IsActive = s.IsActive,
                    DisplayOrder = s.DisplayOrder,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    TaskCount = s.Tasks.Count,
                })
                .ToPagedResultAsync(paging, ct);

            return Ok(page);
        }

        // GET: api/serviceitems/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<ServiceItemDto>> GetById(int id, CancellationToken ct)
        {
            var item = await _context.ServiceItems.AsNoTracking()
                .Include(s => s.SkillCategory)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (item is null) return NotFound();

            var dto = ServiceItemDto.From(item);
            dto.CategoryName = item.SkillCategory?.Name ?? string.Empty;
            dto.TaskCount = await _context.GigTasks.CountAsync(t => t.ServiceItemId == id, ct);

            return Ok(dto);
        }

        // POST: api/serviceitems  -> Id absent creates, Id present updates that row
        [HttpPost]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<ServiceItemDto>> Save(SaveServiceItemRequest request, CancellationToken ct)
        {
            var validationError = await ValidateAsync(request, request.Id, ct);
            if (validationError is not null)
                return UnprocessableEntity(new ProblemDetails { Title = validationError, Status = 422 });

            var isEdit = request.Id is not null;
            ServiceItem item;

            if (isEdit)
            {
                var existing = await _context.ServiceItems.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
                if (existing is null) return NotFound();
                item = existing;
            }
            else
            {
                item = new ServiceItem { CreatedAt = DateTime.UtcNow };
                _context.ServiceItems.Add(item);
            }

            var imageError = await ApplyImageAsync(request.Image, item, ct);
            if (imageError is not null)
                return UnprocessableEntity(new ProblemDetails { Title = imageError, Status = 422 });

            item.SkillCategoryId = request.SkillCategoryId;
            item.Name = request.Name.Trim();
            item.Description = Normalize(request.Description);
            item.BasePayout = request.BasePayout;
            item.AllowsInstantBooking = request.AllowsInstantBooking && request.BasePayout is > 0;
            item.IsActive = request.IsActive;
            item.DisplayOrder = request.DisplayOrder;
            if (isEdit) item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                isEdit ? "Updated service item {ItemId} ({Name})" : "Created service item {ItemId} ({Name})",
                item.Id, item.Name);

            var dto = ServiceItemDto.From(item);
            dto.CategoryName = (await _categories.GetActiveOptionsAsync(ct))
                .FirstOrDefault(c => c.Id == item.SkillCategoryId)?.Name ?? string.Empty;

            return isEdit
                ? Ok(dto)
                : CreatedAtAction(nameof(GetById), new { id = item.Id }, dto);
        }

        // POST: api/serviceitems/5/toggle
        [HttpPost("{id:int}/toggle")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<ServiceItemDto>> Toggle(
            int id, [FromBody] ToggleActiveRequest request, CancellationToken ct)
        {
            var item = await _context.ServiceItems.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (item is null) return NotFound();

            item.IsActive = request.IsActive;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return Ok(ServiceItemDto.From(item));
        }

        // DELETE: api/serviceitems/5  -> only when no task references it
        [HttpDelete("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var item = await _context.ServiceItems.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (item is null) return NotFound();

            var taskCount = await _context.GigTasks.CountAsync(t => t.ServiceItemId == id, ct);

            if (taskCount > 0)
                return Conflict(new ProblemDetails
                {
                    Title = $"'{item.Name}' is used by {taskCount} task(s). Deactivate it instead of deleting it.",
                    Status = 409,
                });

            _context.ServiceItems.Remove(item);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted service item {ItemId} ({Name})", id, item.Name);

            return NoContent();
        }

        private async Task<string?> ValidateAsync(SaveServiceItemRequest request, int? excludingId, CancellationToken ct)
        {
            if (!await _categories.IsSelectableAsync(request.SkillCategoryId, ct))
                return "Choose an active category.";

            var name = request.Name.Trim().ToLower();

            var duplicate = await _context.ServiceItems.AnyAsync(
                s => s.SkillCategoryId == request.SkillCategoryId
                  && s.Name.ToLower() == name
                  && (excludingId == null || s.Id != excludingId), ct);

            if (duplicate)
                return $"'{request.Name.Trim()}' already exists in that category.";

            if (request.AllowsInstantBooking && request.BasePayout is null or <= 0)
                return "Set a partner payout before allowing instant booking.";

            return null;
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task<string?> ApplyImageAsync(IFormFile? image, ServiceItem item, CancellationToken ct)
        {
            if (image is null || image.Length == 0) return null;

            var saved = await _storage.SaveAsync(image, FileCategory.ServiceImage, ct);
            if (!saved.Succeeded) return saved.Error;

            var previous = item.ImageFileName;
            item.ImageFileName = saved.FileName;
            _storage.Delete(previous, FileCategory.ServiceImage);

            return null;
        }
    }
}
