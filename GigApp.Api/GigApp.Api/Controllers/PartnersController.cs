using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Banking;
using GigApp.Api.Services.Bidding;
using GigApp.Api.Services.Booking;
using GigApp.Api.Services.Earnings;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Geo;
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
        private readonly IKycHistoryService _kycHistory;
        private readonly IBidService _bids;
        private readonly IOfferService _offers;
        private readonly IMatchService _match;
        private readonly IAddressService _addresses;
        private readonly IEarningsService _earnings;
        private readonly IBankAccountService _bankAccounts;
        private readonly ILogger<PartnersController> _logger;

        public PartnersController(
            AppDbContext context,
            IFileStorageService storage,
            ICategoryLookup categories,
            IKycHistoryService kycHistory,
            IBidService bids,
            IOfferService offers,
            IMatchService match,
            IAddressService addresses,
            IEarningsService earnings,
            IBankAccountService bankAccounts,
            ILogger<PartnersController> logger)
        {
            _context = context;
            _storage = storage;
            _categories = categories;
            _kycHistory = kycHistory;
            _bids = bids;
            _offers = offers;
            _match = match;
            _addresses = addresses;
            _earnings = earnings;
            _bankAccounts = bankAccounts;
            _logger = logger;
        }

        // GET: api/partners?verified=false  -> Admin review queue (unpaged, for anything that wants the whole set)
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

        // GET: api/partners/all?page=1&pageSize=10&search=&verified=&categoryId=  -> Admin list pages
        [HttpGet("all")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<PagedResult<PartnerDto>>> GetAllPaged(
            [FromQuery] PageRequest paging, bool? verified, int? categoryId, CancellationToken ct)
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

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(p =>
                    p.User!.Name.ToLower().Contains(term) || p.User.Phone.Contains(term));
            }

            var page = await query
                .OrderBy(p => p.KycStatus == KycStatus.Pending ? 0
                            : p.KycStatus == KycStatus.NotSubmitted ? 1
                            : p.KycStatus == KycStatus.Rejected ? 2 : 3)
                .ThenByDescending(p => p.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return Ok(page.Map(PartnerDto.From));
        }

        // GET: api/partners/5/kyc-history
        [HttpGet("{id:int}/kyc-history")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<ActionResult<IReadOnlyList<KycHistoryEntryDto>>> GetKycHistory(
            int id, CancellationToken ct) =>
            Ok(await _kycHistory.ForPartnerAsync(id, ct));

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

        /// <summary>
        /// The caller is this partner, or a customer who has worked with them, or
        /// who has an open bid from them to decide on — a customer reviewing a
        /// bidder's profile before accepting is the whole point of showing it.
        /// </summary>
        private async Task<bool> MayViewPartnerAsync(int partnerId, CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();

            if (User.IsInRole(UserRoles.Partner))
                return await _context.Partners.AnyAsync(p => p.Id == partnerId && p.UserId == userId, ct);

            if (await _context.GigTasks.AnyAsync(t => t.PartnerId == partnerId && t.CustomerId == userId, ct))
                return true;

            return await _context.TaskBids.AnyAsync(
                b => b.PartnerId == partnerId && b.GigTask!.CustomerId == userId, ct);
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

        // GET: api/partners/me/dashboard
        [HttpGet("me/dashboard")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<ProviderDashboardDto>> GetMyDashboard(CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: false, ct);
            var dto = new ProviderDashboardDto();

            if (partner is null) return Ok(dto);

            dto.Profile = PartnerDto.From(partner);

            // An unverified partner keeps the jobs they already hold, so a skill
            // change cannot strand a customer mid-booking. Everything else on
            // the dashboard stays hidden until they are approved.
            var jobs = _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Customer)
                .Include(t => t.Category)
                .Include(t => t.ServiceItem)
                .Where(t => t.PartnerId == partner.Id);

            if (!partner.IsVerified)
            {
                jobs = jobs.Where(t => t.Status != GigTaskStatus.Completed
                                     && t.Status != GigTaskStatus.Cancelled);
            }

            var myJobs = await jobs.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
            dto.MyJobs = myJobs.Select(GigTaskDto.From).ToList();

            if (!partner.IsVerified) return Ok(dto);

            dto.MyBids = await _bids.ForPartnerAsync(partner.UserId, ct);

            // Already-bid tasks move to "My bids", so drop them here to avoid
            // the partner thinking they still need to act.
            var alreadyBidTaskIds = dto.MyBids
                .Where(b => b.IsOpen || b.Status == BidStatus.Accepted)
                .Select(b => b.GigTaskId)
                .ToHashSet();

            var open = _context.GigTasks
                .AsNoTracking()
                .Include(t => t.Customer)
                .Include(t => t.Category)
                .Include(t => t.ServiceItem)
                .Where(t => t.Status == GigTaskStatus.Pending
                         && t.CategoryId == partner.SkillCategoryId
                         && !_context.TaskCancellations.Any(
                                c => c.GigTaskId == t.Id && c.PartnerId == partner.Id));

            // Work outside the radius the partner set is not work they will
            // take, so PostGIS drops it before it is ever loaded. A task with
            // no pin stays in — an old booking without one is still worth doing.
            if (partner.BaseLocation is not null)
            {
                var radiusMetres = partner.ServiceRadiusKm * GeoPoint.MetresPerKm;

                open = open.Where(t => t.Location == null
                                     || t.Location.Distance(partner.BaseLocation) <= radiusMetres);
            }

            // Urgent work first, then newest — a same-day job is no use to
            // anyone sitting three pages down.
            var available = await open
                .OrderBy(t => t.Urgency == TaskUrgency.Urgent ? 0
                            : t.Urgency == TaskUrgency.Normal ? 1 : 2)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            var shortlist = available.Where(t => !alreadyBidTaskIds.Contains(t.Id)).ToList();

            var distances = await _match.DistancesFromPartnerAsync(
                partner.Id, shortlist.Select(t => t.Id), ct);

            dto.AvailableTasks = shortlist
                .Select(t =>
                {
                    var taskDto = GigTaskDto.From(t);
                    taskDto.DistanceKm = distances.TryGetValue(t.Id, out var km) ? km : null;
                    return taskDto;
                })
                .OrderBy(t => t.Urgency == TaskUrgency.Urgent ? 0
                            : t.Urgency == TaskUrgency.Normal ? 1 : 2)
                .ThenBy(t => t.DistanceKm ?? double.MaxValue)
                .ThenByDescending(t => t.CreatedAt)
                .ToList();

            dto.HasServiceArea = partner.BaseLocation is not null;
            dto.ServiceRadiusKm = partner.ServiceRadiusKm;
            dto.Offer = await _offers.LiveOfferForPartnerAsync(partner.Id, ct);

            return Ok(dto);
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

        // GET: api/partners/me/kyc-history
        [HttpGet("me/kyc-history")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<IReadOnlyList<KycHistoryEntryDto>>> GetMyKycHistory(CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: false, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            return Ok(await _kycHistory.ForPartnerAsync(partner.Id, ct));
        }

        // GET: api/partners/me/earnings
        [HttpGet("me/earnings")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<ProviderEarningsDto>> GetMyEarnings(CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: false, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            return Ok(new ProviderEarningsDto
            {
                Summary = await _earnings.GetSummaryAsync(partner.Id, ct),
                BankAccount = await _bankAccounts.GetAsync(partner.UserId, ct),
            });
        }

        // GET: api/partners/me/earnings/entries?page=&pageSize=&entryType=
        [HttpGet("me/earnings/entries")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<PagedResult<LedgerEntryDto>>> GetMyEarningsEntries(
            [FromQuery] PageRequest paging, string? entryType, CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: false, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            return Ok(await _earnings.GetEntriesAsync(partner.Id, paging, entryType, ct));
        }

        // GET: api/partners/me/service-area
        [HttpGet("me/service-area")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<ServiceAreaDto>> GetMyServiceArea(CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: false, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            var dto = new ServiceAreaDto
            {
                Form = new UpdateServiceAreaRequest
                {
                    BaseLatitude = partner.BaseLatitude,
                    BaseLongitude = partner.BaseLongitude,
                    ServiceRadiusKm = partner.ServiceRadiusKm,
                    BaseCity = partner.BaseCity,
                    BasePincode = partner.BasePincode,
                },
                Addresses = await _addresses.ListAsync(partner.UserId, ct),
                OpenTasksInRange = partner.BaseLocation is null
                    ? 0
                    : await _context.GigTasks.CountAsync(
                        t => t.Status == GigTaskStatus.Pending
                          && t.CategoryId == partner.SkillCategoryId
                          && t.Location != null
                          && t.Location.Distance(partner.BaseLocation)
                             <= partner.ServiceRadiusKm * GeoPoint.MetresPerKm, ct),
            };

            return Ok(dto);
        }

        // PUT: api/partners/me/service-area
        [HttpPut("me/service-area")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [TrackForm("ServiceArea")]
        public async Task<ActionResult<ServiceAreaDto>> UpdateMyServiceArea(
            UpdateServiceAreaRequest request, CancellationToken ct)
        {
            var partner = await LoadOwnProfileAsync(track: true, ct);
            if (partner is null) return NotFound("No partner profile is attached to this account.");

            var hasPin = request.BaseLatitude is not null && request.BaseLongitude is not null;

            if (!hasPin && (request.BaseLatitude is not null || request.BaseLongitude is not null))
                return BadRequest(new ProblemDetails
                {
                    Title = "A location needs both a latitude and a longitude.",
                    Status = 400,
                });

            partner.BaseLatitude = request.BaseLatitude;
            partner.BaseLongitude = request.BaseLongitude;
            partner.BaseLocation = GeoPoint.From(request.BaseLatitude, request.BaseLongitude);
            partner.ServiceRadiusKm = request.ServiceRadiusKm;
            partner.BaseCity = string.IsNullOrWhiteSpace(request.BaseCity) ? null : request.BaseCity.Trim();
            partner.BasePincode = string.IsNullOrWhiteSpace(request.BasePincode) ? null : request.BasePincode.Trim();

            await _context.SaveChangesAsync(ct);

            return await GetMyServiceArea(ct);
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
