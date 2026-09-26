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
    [Authorize(Policy = Policies.AdminOnly)]
    public class BannersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFileStorageService _storage;
        private readonly ILogger<BannersController> _logger;

        public BannersController(AppDbContext context, IFileStorageService storage, ILogger<BannersController> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        // GET: api/banners?page=1&pageSize=10
        [HttpGet]
        public async Task<ActionResult<PagedResult<BannerDto>>> GetAll([FromQuery] PageRequest paging, CancellationToken ct)
        {
            var page = await _context.Banners
                .AsNoTracking()
                .OrderBy(b => b.Placement).ThenBy(b => b.SortOrder).ThenBy(b => b.Title)
                .ToPagedResultAsync(paging, ct);

            return Ok(page.Map(BannerDto.From));
        }

        // GET: api/banners/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<BannerDto>> GetById(int id, CancellationToken ct)
        {
            var banner = await _context.Banners.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
            if (banner is null) return NotFound();

            return Ok(BannerDto.From(banner));
        }

        // POST: api/banners  -> Id absent creates, Id present updates that row
        [HttpPost]
        public async Task<ActionResult<BannerDto>> Save(SaveBannerRequest request, CancellationToken ct)
        {
            if (!BannerPlacements.IsValid(request.Placement))
                ModelState.AddModelError(nameof(request.Placement), "Choose a valid placement.");

            if (!string.IsNullOrWhiteSpace(request.LinkUrl) && !Url.IsLocalUrl(request.LinkUrl))
                ModelState.AddModelError(nameof(request.LinkUrl), "Link to a page on this site, starting with a slash.");

            if (request.StartsAt is not null && request.EndsAt is not null && request.EndsAt < request.StartsAt)
                ModelState.AddModelError(nameof(request.EndsAt), "The end date is before the start date.");

            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var isEdit = request.Id is not null;
            Banner banner;

            if (isEdit)
            {
                var existing = await _context.Banners.FirstOrDefaultAsync(b => b.Id == request.Id, ct);
                if (existing is null) return NotFound();
                banner = existing;
            }
            else
            {
                banner = new Banner { CreatedAt = DateTime.UtcNow };
                _context.Banners.Add(banner);
            }

            var imageError = await ApplyImageAsync(request.Image, banner, ct);
            if (imageError is not null)
                return UnprocessableEntity(new ProblemDetails { Title = imageError, Status = 422 });

            if (banner.ImageFileName is null)
                return UnprocessableEntity(new ProblemDetails { Title = "A banner needs an image.", Status = 422 });

            banner.Title = request.Title.Trim();
            banner.Subtitle = Normalize(request.Subtitle);
            banner.CallToAction = Normalize(request.CallToAction);
            banner.LinkUrl = Normalize(request.LinkUrl);
            banner.Placement = request.Placement;
            banner.SortOrder = request.SortOrder;
            banner.IsActive = request.IsActive;
            banner.StartsAt = request.StartsAt.ToUtc();
            banner.EndsAt = request.EndsAt.ToUtc();
            if (isEdit) banner.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                isEdit ? "Updated banner {BannerId} ({Title})" : "Created banner {BannerId} ({Title})",
                banner.Id, banner.Title);

            return isEdit
                ? Ok(BannerDto.From(banner))
                : CreatedAtAction(nameof(GetById), new { id = banner.Id }, BannerDto.From(banner));
        }

        // DELETE: api/banners/5  -> also removes the image file
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var banner = await _context.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (banner is null) return NotFound();

            _storage.Delete(banner.ImageFileName, FileCategory.BannerImage);
            _context.Banners.Remove(banner);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted banner {BannerId} ({Title})", id, banner.Title);

            return NoContent();
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task<string?> ApplyImageAsync(IFormFile? image, Banner banner, CancellationToken ct)
        {
            if (image is null || image.Length == 0) return null;

            var saved = await _storage.SaveAsync(image, FileCategory.BannerImage, ct);
            if (!saved.Succeeded) return saved.Error;

            var previous = banner.ImageFileName;
            banner.ImageFileName = saved.FileName;
            _storage.Delete(previous, FileCategory.BannerImage);

            return null;
        }
    }
}
