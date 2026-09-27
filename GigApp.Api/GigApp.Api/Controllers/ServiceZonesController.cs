using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services.Geo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceZonesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ServiceZonesController> _logger;

        public ServiceZonesController(AppDbContext context, ILogger<ServiceZonesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/servicezones -> active list for the customer's own picker.
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceZoneOptionDto>>> GetActive(CancellationToken ct)
        {
            var options = await _context.ServiceZones
                .AsNoTracking()
                .Where(z => z.IsActive)
                .OrderBy(z => z.DisplayOrder).ThenBy(z => z.Name)
                .Select(z => new ServiceZoneOptionDto { Id = z.Id, Name = z.Name })
                .ToListAsync(ct);

            return Ok(options);
        }

        // GET: api/servicezones/nearest?lat=&lng= -> the zone whose circle the point falls inside, nearest first.
        [HttpGet("nearest")]
        [AllowAnonymous]
        public async Task<ActionResult<NearestZoneDto>> Nearest(double lat, double lng, CancellationToken ct)
        {
            var point = GeoPoint.From(lat, lng);

            var candidates = await _context.ServiceZones
                .AsNoTracking()
                .Where(z => z.IsActive && z.Center != null)
                .Select(z => new { z.Id, z.Name, z.RadiusKm, Metres = z.Center!.Distance(point) })
                .ToListAsync(ct);

            var nearest = candidates
                .Where(z => z.Metres <= z.RadiusKm * GeoPoint.MetresPerKm)
                .OrderBy(z => z.Metres)
                .FirstOrDefault();

            if (nearest is null)
                return NotFound(new ProblemDetails { Title = "We do not serve this area yet.", Status = 404 });

            return Ok(new NearestZoneDto
            {
                Id = nearest.Id,
                Name = nearest.Name,
                DistanceKm = GeoPoint.KmFromMetres(nearest.Metres),
            });
        }

        // GET: api/servicezones/all?page=1&pageSize=10
        [HttpGet("all")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<PagedResult<ServiceZoneDto>>> GetAll(
            [FromQuery] PageRequest paging, CancellationToken ct)
        {
            IQueryable<ServiceZone> query = _context.ServiceZones.AsNoTracking().Include(z => z.Categories);

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(z => z.Name.ToLower().Contains(term));
            }

            var page = await query
                .OrderBy(z => z.DisplayOrder).ThenBy(z => z.Name)
                .ToPagedResultAsync(paging, ct);

            return Ok(page.Map(ServiceZoneDto.From));
        }

        // GET: api/servicezones/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<ServiceZoneDto>> GetById(int id, CancellationToken ct)
        {
            var zone = await _context.ServiceZones
                .AsNoTracking()
                .Include(z => z.Categories)
                .FirstOrDefaultAsync(z => z.Id == id, ct);

            if (zone is null) return NotFound();

            return Ok(ServiceZoneDto.From(zone));
        }

        // POST: api/servicezones -> Id absent creates, Id present updates that row
        [HttpPost]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<ServiceZoneDto>> Save(SaveServiceZoneRequest request, CancellationToken ct)
        {
            var isEdit = request.Id is not null;
            ServiceZone zone;

            if (isEdit)
            {
                var existing = await _context.ServiceZones
                    .Include(z => z.Categories)
                    .FirstOrDefaultAsync(z => z.Id == request.Id, ct);

                if (existing is null) return NotFound();
                zone = existing;
                zone.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                zone = new ServiceZone { CreatedAt = DateTime.UtcNow };
                _context.ServiceZones.Add(zone);
            }

            zone.Name = request.Name.Trim();
            zone.Center = GeoPoint.From(request.Latitude, request.Longitude);
            zone.RadiusKm = request.RadiusKm;
            zone.IsActive = request.IsActive;
            zone.DisplayOrder = request.DisplayOrder;

            var wanted = request.CategoryIds.Distinct().ToList();
            foreach (var stale in zone.Categories.Where(c => !wanted.Contains(c.SkillCategoryId)).ToList())
                zone.Categories.Remove(stale);

            foreach (var categoryId in wanted)
            {
                if (zone.Categories.Any(c => c.SkillCategoryId == categoryId)) continue;
                zone.Categories.Add(new ServiceZoneCategory { SkillCategoryId = categoryId });
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                isEdit ? "Updated service zone {ZoneId} ({Name})" : "Created service zone {ZoneId} ({Name})",
                zone.Id, zone.Name);

            return isEdit
                ? Ok(ServiceZoneDto.From(zone))
                : CreatedAtAction(nameof(GetById), new { id = zone.Id }, ServiceZoneDto.From(zone));
        }

        // POST: api/servicezones/5/toggle
        [HttpPost("{id:int}/toggle")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<ServiceZoneDto>> Toggle(
            int id, [FromBody] ToggleActiveRequest request, CancellationToken ct)
        {
            var zone = await _context.ServiceZones.Include(z => z.Categories).FirstOrDefaultAsync(z => z.Id == id, ct);
            if (zone is null) return NotFound();

            zone.IsActive = request.IsActive;
            zone.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return Ok(ServiceZoneDto.From(zone));
        }

        // DELETE: api/servicezones/5
        [HttpDelete("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var zone = await _context.ServiceZones.FirstOrDefaultAsync(z => z.Id == id, ct);
            if (zone is null) return NotFound();

            _context.ServiceZones.Remove(zone);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted service zone {ZoneId} ({Name})", id, zone.Name);

            return NoContent();
        }
    }
}
