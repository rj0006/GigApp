using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// The skill category master. Any signed-in user can read the active list
    /// (it feeds every category picker); only admins can change it.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SkillCategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SkillCategoriesController> _logger;

        public SkillCategoriesController(AppDbContext context, ILogger<SkillCategoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/skillcategories  -> active list for autocomplete pickers
        [HttpGet]
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

        // GET: api/skillcategories/all  -> full master including inactive
        [HttpGet("all")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<IEnumerable<SkillCategoryDto>>> GetAll(CancellationToken ct)
        {
            var categories = await _context.SkillCategories
                .AsNoTracking()
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new SkillCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    DisplayOrder = c.DisplayOrder,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    PartnerCount = c.Partners.Count,
                    TaskCount = c.Tasks.Count,
                })
                .ToListAsync(ct);

            return Ok(categories);
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

        // POST: api/skillcategories
        [HttpPost]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<SkillCategoryDto>> Create(
            SaveSkillCategoryRequest request, CancellationToken ct)
        {
            var name = request.Name.Trim();

            if (await NameExistsAsync(name, excludingId: null, ct))
                return Conflict(new ProblemDetails { Title = $"'{name}' already exists.", Status = 409 });

            var category = new SkillCategory
            {
                Name = name,
                Description = Normalize(request.Description),
                IsActive = request.IsActive,
                DisplayOrder = request.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
            };

            _context.SkillCategories.Add(category);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Created skill category {CategoryId} ({Name})", category.Id, category.Name);

            return CreatedAtAction(nameof(GetById), new { id = category.Id }, SkillCategoryDto.From(category));
        }

        // PUT: api/skillcategories/5
        [HttpPut("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<SkillCategoryDto>> Update(
            int id, SaveSkillCategoryRequest request, CancellationToken ct)
        {
            var category = await _context.SkillCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return NotFound();

            var name = request.Name.Trim();

            if (await NameExistsAsync(name, excludingId: id, ct))
                return Conflict(new ProblemDetails { Title = $"'{name}' already exists.", Status = 409 });

            category.Name = name;
            category.Description = Normalize(request.Description);
            category.IsActive = request.IsActive;
            category.DisplayOrder = request.DisplayOrder;
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
    }
}
