using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PartnersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFileStorageService _storage;
        private readonly ICategoryLookup _categories;
        private readonly ILogger<PartnersController> _logger;

        public PartnersController(
            AppDbContext context,
            IFileStorageService storage,
            ICategoryLookup categories,
            ILogger<PartnersController> logger)
        {
            _context = context;
            _storage = storage;
            _categories = categories;
            _logger = logger;
        }

        // GET: api/partners?verified=false  -> Admin review queue
        [HttpGet]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<IEnumerable<PartnerDto>>> GetPartners(
            [FromQuery] bool? verified,
            [FromQuery] int? categoryId,
            CancellationToken ct)
        {
            var query = _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .AsQueryable();

            if (verified is not null)
                query = query.Where(p => (verified == true ? p.KycStatus == KycStatus.Approved : p.KycStatus != KycStatus.Approved));

            if (categoryId is not null)
                query = query.Where(p => p.SkillCategoryId == categoryId);

            var partners = await query
                .OrderBy(p => p.KycStatus == KycStatus.Approved ? 1 : 0)      // unverified first — that is the work queue
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync(ct);

            return Ok(partners.Select(PartnerDto.From));
        }

        // GET: api/partners/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<PartnerDto>> GetPartnerById(int id, CancellationToken ct)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (partner is null) return NotFound();

            return Ok(PartnerDto.From(partner));
        }

        // GET: api/partners/5/public  -> what a customer may see about their partner
        [HttpGet("{id:int}/public")]
        public async Task<ActionResult<PartnerPublicDto>> GetPublicProfile(int id, CancellationToken ct)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (partner is null) return NotFound();

            // Anyone signed in could otherwise walk the ids and harvest partner
            // phone numbers, so a customer must actually share a task with them.
            if (!User.IsAdmin() && !await MayViewPartnerAsync(partner.Id, ct))
                return Forbid();

            var completedJobs = await _context.GigTasks.CountAsync(
                t => t.PartnerId == partner.Id && t.Status == GigTaskStatus.Completed, ct);

            return Ok(PartnerPublicDto.From(partner, completedJobs));
        }

        /// <summary>The caller is this partner, or a customer they have worked for.</summary>
        private async Task<bool> MayViewPartnerAsync(int partnerId, CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();

            if (User.IsInRole(UserRoles.Partner))
                return await _context.Partners.AnyAsync(p => p.Id == partnerId && p.UserId == userId, ct);

            return await _context.GigTasks.AnyAsync(
                t => t.PartnerId == partnerId && t.CustomerId == userId, ct);
        }

        // GET: api/partners/me
        [HttpGet("me")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<PartnerDto>> GetMyProfile(CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: false, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            return Ok(PartnerDto.From(partner));
        }

        // PUT: api/partners/me  -> Partner edits their own skill category
        [HttpPut("me")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [TrackForm(KycHistoryService.FormType)]
        public async Task<ActionResult<PartnerDto>> UpdateMyProfile(
            UpdatePartnerProfileRequest request, CancellationToken ct)
        {
            var categoryIsSelectable = await _context.SkillCategories
                .AnyAsync(c => c.Id == request.SkillCategoryId && c.IsActive, ct);

            if (!categoryIsSelectable)
                return BadRequest(new ProblemDetails { Title = "Choose a valid skill category.", Status = 400 });

            var partner = await LoadOwnProfileAsync(track: true, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            var fromName = await _categories.GetNameAsync(partner.SkillCategoryId, ct);
            var toName = await _categories.GetNameAsync(request.SkillCategoryId, ct);

            PartnerKyc.ChangeSkill(partner, request.SkillCategoryId, fromName, toName);
            await _context.SaveChangesAsync(ct);

            await _context.Entry(partner).Reference(p => p.SkillCategory).LoadAsync(ct);

            return Ok(PartnerDto.From(partner));
        }

        // PUT: api/partners/me/availability  -> Partner goes on or off duty
        [HttpPut("me/availability")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<PartnerDto>> UpdateMyAvailability(
            UpdateAvailabilityRequest request, CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: true, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            partner.IsAvailable = request.IsAvailable;
            await _context.SaveChangesAsync(ct);

            return Ok(PartnerDto.From(partner));
        }

        // POST: api/partners/me/kyc  -> Partner uploads a document for review
        [HttpPost("me/kyc")]
        [TrackEntry(TrackingEntryType.Update)]
        [TrackForm(KycHistoryService.FormType)]
        [Authorize(Policy = Policies.PartnerOnly)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<PartnerDto>> SubmitKyc(
            [FromForm] UpdateKycRequest request, CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: true, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            if (!request.HasAnything)
                return BadRequest(new ProblemDetails { Title = "Nothing to update.", Status = 400 });

            // Replace only what was actually sent; keep the rest as it is.
            var replaced = new List<string>();

            var selfie = await ReplaceAsync(request.Selfie, partner.SelfieFileName, replaced, ct);
            if (selfie.Error is not null) return BadRequest(new ProblemDetails { Title = selfie.Error, Status = 400 });

            var front = await ReplaceAsync(request.AadhaarFront, partner.AadhaarFrontFileName, replaced, ct);
            if (front.Error is not null) return BadRequest(new ProblemDetails { Title = front.Error, Status = 400 });

            var back = await ReplaceAsync(request.AadhaarBack, partner.AadhaarBackFileName, replaced, ct);
            if (back.Error is not null) return BadRequest(new ProblemDetails { Title = back.Error, Status = 400 });

            partner.SelfieFileName = selfie.FileName;
            partner.AadhaarFrontFileName = front.FileName;
            partner.AadhaarBackFileName = back.FileName;

            if (!string.IsNullOrWhiteSpace(request.AadhaarNumber))
                partner.AadhaarNumber = request.AadhaarNumber.Trim();

            PartnerKyc.SubmitDocuments(partner);

            await _context.SaveChangesAsync(ct);

            // Only after the row is safely updated — otherwise a failed save
            // would leave the partner pointing at a file that no longer exists.
            foreach (var old in replaced) _storage.Delete(old, FileCategory.KycDocument);

            _logger.LogInformation("Partner {PartnerId} submitted KYC for review", partner.Id);

            return Ok(PartnerDto.From(partner));
        }

        // PUT: api/partners/5/verify  -> Admin approves or revokes verification
        [HttpPut("{id:int}/verify")]
        [Authorize(Policy = Policies.AdminOnly)]
        [TrackForm(KycHistoryService.FormType)]
        public async Task<ActionResult<PartnerDto>> SetVerification(
            int id, VerifyPartnerRequest request, CancellationToken ct)
        {
            var partner = await _context.Partners
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (partner is null) return NotFound();

            // Approval is only meaningful once every document is on file.
            if (request.IsVerified && !partner.HasCompleteKyc)
                return BadRequest(new ProblemDetails
                {
                    Title = "This partner has not submitted a selfie, both Aadhaar sides and an Aadhaar number yet.",
                    Status = 400,
                });

            // Rejecting without saying why leaves the partner unable to work and
            // with nothing to fix, so the reason is mandatory.
            if (!request.IsVerified && string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new ProblemDetails
                {
                    Title = "Give a reason when rejecting a partner.",
                    Status = 400,
                });

            PartnerKyc.Review(partner, request.IsVerified, request.Reason);

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Admin {AdminId} set partner {PartnerId} verified={IsVerified}",
                User.GetRequiredUserId(), partner.Id, request.IsVerified);

            return Ok(PartnerDto.From(partner));
        }

        /// <summary>
        /// Stores a replacement image if one was sent, otherwise keeps the
        /// existing name. Old names go into <paramref name="replaced"/> so they
        /// can be deleted once the row is safely saved.
        /// </summary>
        private async Task<(string? FileName, string? Error)> ReplaceAsync(
            IFormFile? file, string? current, List<string> replaced, CancellationToken ct)
        {
            if (file is null) return (current, null);

            var saved = await _storage.SaveAsync(file, FileCategory.KycDocument, ct);
            if (!saved.Succeeded) return (current, saved.Error);

            if (!string.IsNullOrWhiteSpace(current)) replaced.Add(current);

            return (saved.FileName, null);
        }

        private async Task<Partner?> LoadOwnProfileAsync(bool track, CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();

            var query = _context.Partners
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .AsQueryable();

            if (!track) query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        }
    }
}
