using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SkillCategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFileStorageService _storage;
        private readonly ILogger<SkillCategoriesController> _logger;

        public SkillCategoriesController(
            AppDbContext context, IFileStorageService storage, ILogger<SkillCategoriesController> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        // GET: api/skillcategories  -> active list for autocomplete pickers.
        // Anonymous on purpose: the same names are already public on the
        // storefront and on the anonymous partner registration page, so this
        // exposes nothing new — it just gives a mobile client, which has no
        // page to server-render options into, the same list as JSON.
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<SkillCategoryOptionDto>>> GetActive(
            [FromQuery] string? search, CancellationToken ct)
        {
            var query = _context.SkillCategories.AsNoTracking().Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(term));
            }

            var options = await query
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new SkillCategoryOptionDto { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);

            return Ok(options);
        }

        // GET: api/skillcategories/all?page=1&pageSize=10&search=&showInactive=true
        [HttpGet("all")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<PagedResult<SkillCategoryDto>>> GetAll(
            [FromQuery] PageRequest paging, bool showInactive, CancellationToken ct)
        {
            var query = _context.SkillCategories.AsNoTracking();
            if (!showInactive) query = query.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(term));
            }

            var page = await query
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new SkillCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ImageFileName = c.ImageFileName,
                    IsActive = c.IsActive,
                    DisplayOrder = c.DisplayOrder,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    PartnerCount = c.Partners.Count,
                    TaskCount = c.Tasks.Count,
                })
                .ToPagedResultAsync(paging, ct);

            return Ok(page);
        }

        // GET: api/skillcategories/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<SkillCategoryDto>> GetById(int id, CancellationToken ct)
        {
            var category = await _context.SkillCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category is null) return NotFound();

            return Ok(SkillCategoryDto.From(category));
        }

        // POST: api/skillcategories  -> Id absent creates, Id present updates that row
        [HttpPost]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<SkillCategoryDto>> Save(
            SaveSkillCategoryRequest request, CancellationToken ct)
        {
            var name = request.Name.Trim();
            var isEdit = request.Id is not null;

            if (await NameExistsAsync(name, request.Id, ct))
                return Conflict(new ProblemDetails { Title = $"'{name}' already exists.", Status = 409 });

            SkillCategory category;

            if (isEdit)
            {
                var existing = await _context.SkillCategories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
                if (existing is null) return NotFound();
                category = existing;
            }
            else
            {
                category = new SkillCategory { CreatedAt = DateTime.UtcNow };
                _context.SkillCategories.Add(category);
            }

            var imageError = await ApplyImageAsync(request.Image, category, ct);
            if (imageError is not null)
                return UnprocessableEntity(new ProblemDetails { Title = imageError, Status = 422 });

            category.Name = name;
            category.Description = Normalize(request.Description);
            category.IsActive = request.IsActive;
            category.DisplayOrder = request.DisplayOrder;
            if (isEdit) category.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(isEdit ? "Updated skill category {CategoryId} ({Name})" : "Created skill category {CategoryId} ({Name})",
                category.Id, category.Name);

            return isEdit
                ? Ok(SkillCategoryDto.From(category))
                : CreatedAtAction(nameof(GetById), new { id = category.Id }, SkillCategoryDto.From(category));
        }

        // POST: api/skillcategories/5/toggle
        [HttpPost("{id:int}/toggle")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<SkillCategoryDto>> Toggle(
            int id, [FromBody] ToggleActiveRequest request, CancellationToken ct)
        {
            var category = await _context.SkillCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return NotFound();

            category.IsActive = request.IsActive;
            category.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return Ok(SkillCategoryDto.From(category));
        }

        // DELETE: api/skillcategories/5  -> only when nothing references it
        [HttpDelete("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var category = await _context.SkillCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return NotFound();

            var partnerCount = await _context.Partners.CountAsync(p => p.SkillCategoryId == id, ct);
            var taskCount = await _context.GigTasks.CountAsync(t => t.CategoryId == id, ct);

            // Deleting would orphan live records, so deactivating is the way out.
            if (partnerCount > 0 || taskCount > 0)
                return Conflict(new ProblemDetails
                {
                    Title = $"'{category.Name}' is used by {partnerCount} partner(s) and {taskCount} task(s). "
                          + "Deactivate it instead of deleting it.",
                    Status = 409,
                });

            _context.SkillCategories.Remove(category);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted skill category {CategoryId} ({Name})", id, category.Name);

            return NoContent();
        }

        private Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken ct) =>
            _context.SkillCategories.AnyAsync(
                c => c.Name.ToLower() == name.ToLower() && (excludingId == null || c.Id != excludingId), ct);

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task<string?> ApplyImageAsync(IFormFile? image, SkillCategory category, CancellationToken ct)
        {
            if (image is null || image.Length == 0) return null;

            var saved = await _storage.SaveAsync(image, FileCategory.CategoryImage, ct);
            if (!saved.Succeeded) return saved.Error;

            var previous = category.ImageFileName;
            category.ImageFileName = saved.FileName;
            _storage.Delete(previous, FileCategory.CategoryImage);

            return null;
        }
    }
}
