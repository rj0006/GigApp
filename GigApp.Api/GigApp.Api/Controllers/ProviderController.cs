using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Banking;
using GigApp.Api.Services.Bidding;
using GigApp.Api.Services.Booking;
using GigApp.Api.Services.Earnings;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Geo;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Orders;
using GigApp.Api.Services.Ratings;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Tracking;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("provider")]
    public class ProviderController : PortalControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;
        private readonly IFileStorageService _storage;
        private readonly IBidService _bids;
        private readonly ITaskClaimService _claims;
        private readonly IMatchService _match;
        private readonly IKycHistoryService _kycHistory;
        private readonly IEarningsService _earnings;

        public ProviderController(
            IAuthService authService,
            IProfileService profileService,
            IAddressService addressService,
            AppDbContext context,
            ICategoryLookup categories,
            IFileStorageService storage,
            IBidService bids,
            ITaskClaimService claims,
            IMatchService match,
            IRatingService ratings,
            IBankAccountService bankAccounts,
            IKycHistoryService kycHistory,
            IEarningsService earnings,
            IOrderHistoryService orderHistory,
            ISupportService support)
            : base(authService, profileService, addressService, bankAccounts, orderHistory, support, ratings)
        {
            _context = context;
            _categories = categories;
            _storage = storage;
            _bids = bids;
            _claims = claims;
            _match = match;
            _kycHistory = kycHistory;
            _earnings = earnings;
        }

        protected override string PortalSlug => "provider";
        protected override string RequiredRole => UserRoles.Partner;

        private string KycPath => $"{ProfilePath}/{ProfileSections.Kyc}";

        [HttpGet("profile/kyc")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public Task<IActionResult> ProfileKyc(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Kyc, ct);

        [HttpGet("profile/earnings")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public Task<IActionResult> ProfileEarnings(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Earnings, ct);

        [HttpGet("profile/area")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public Task<IActionResult> ProfileServiceArea(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.ServiceArea, ct);

        [HttpPost("profile/area")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("ServiceArea")]
        public async Task<IActionResult> UpdateServiceArea(
            UpdateServiceAreaRequest form, CancellationToken ct)
        {
            var areaPath = $"{ProfilePath}/{ProfileSections.ServiceArea}";

            if (!ModelState.IsValid)
            {
                TempData["Error"] = FirstModelError();
                return Redirect(areaPath);
            }

            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.UserId == User.GetRequiredUserId(), ct);

            if (partner is null)
            {
                TempData["Error"] = "No partner profile is attached to this account.";
                return Redirect(areaPath);
            }

            var hasPin = form.BaseLatitude is not null && form.BaseLongitude is not null;

            if (!hasPin && (form.BaseLatitude is not null || form.BaseLongitude is not null))
            {
                TempData["Error"] = "A location needs both a latitude and a longitude.";
                return Redirect(areaPath);
            }

            partner.BaseLatitude = form.BaseLatitude;
            partner.BaseLongitude = form.BaseLongitude;
            partner.BaseLocation = GeoPoint.From(form.BaseLatitude, form.BaseLongitude);
            partner.ServiceRadiusKm = form.ServiceRadiusKm;
            partner.BaseCity = string.IsNullOrWhiteSpace(form.BaseCity) ? null : form.BaseCity.Trim();
            partner.BasePincode = string.IsNullOrWhiteSpace(form.BasePincode) ? null : form.BasePincode.Trim();

            await _context.SaveChangesAsync(ct);

            TrackDoc(partner.Id, PartnerDto.From(partner));

            TempData["Success"] = hasPin
                ? $"Saved. You will now see work within {form.ServiceRadiusKm} km of your base, nearest first."
                : "Saved. Add a location pin to see work sorted by how near it is.";

            return Redirect(areaPath);
        }

        protected override async Task<ProfileExtras> LoadProfileExtrasAsync(
            string section, CancellationToken ct)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .FirstOrDefaultAsync(p => p.UserId == User.GetRequiredUserId(), ct);

            if (partner is null) return new ProfileExtras();

            var extras = new ProfileExtras
            {
                Partner = PartnerDto.From(partner),
                KycHistory = section == ProfileSections.Kyc
                    ? await _kycHistory.ForPartnerAsync(partner.Id, ct)
                    : Array.Empty<KycHistoryEntryDto>(),
            };

            if (section == ProfileSections.Earnings)
            {
                var paging = new PageRequest();
                var entryType = Request.Query["entryType"].ToString();

                extras.Earnings = new PartnerEarningsViewModel
                {
                    Summary = await _earnings.GetSummaryAsync(partner.Id, ct),
                    Entries = await _earnings.GetEntriesAsync(partner.Id, paging, entryType, ct),
                    EntryTypeFilter = entryType,
                    BankAccount = await BankAccounts.GetAsync(partner.UserId, ct),
                };
            }

            if (section == ProfileSections.ServiceArea)
            {
                extras.ServiceArea = new ServiceAreaViewModel
                {
                    Form = new UpdateServiceAreaRequest
                    {
                        BaseLatitude = partner.BaseLatitude,
                        BaseLongitude = partner.BaseLongitude,
                        ServiceRadiusKm = partner.ServiceRadiusKm,
                        BaseCity = partner.BaseCity,
                        BasePincode = partner.BasePincode,
                    },
                    Addresses = await AddressService.ListAsync(partner.UserId, ct),
                    OpenTasksInRange = partner.BaseLocation is null
                        ? 0
                        : await _context.GigTasks.CountAsync(
                            t => t.Status == GigTaskStatus.Pending
                              && t.CategoryId == partner.SkillCategoryId
                              && t.Location != null
                              && t.Location.Distance(partner.BaseLocation)
                                 <= partner.ServiceRadiusKm * GeoPoint.MetresPerKm, ct),
                };
            }

            return extras;
        }

        [HttpGet("login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl, bool denied = false)
        {
            // Already signed in as a partner — no reason to show the form again.
            if (IsAlreadySignedIn) return RedirectToLocalOr(returnUrl);

            ViewData["Title"] = "Partner sign in";
            return View(BuildLoginModel(returnUrl, denied));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [SkipTracking]   // signing in changes no data
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["Title"] = "Partner sign in";
            if (!ModelState.IsValid) return View(model);

            var failed = await SignInAsync(
                () => AuthService.LoginAsync(new LoginRequest
                {
                    Identifier = model.Identifier,
                    Password = model.Password,
                    Role = RequiredRole,
                }),
                nameof(Login), model);

            return failed ?? RedirectToLocalOr(model.ReturnUrl);
        }

        [HttpGet("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(CancellationToken ct)
        {
            if (IsAlreadySignedIn) return Redirect(DashboardPath);

            ViewData["Title"] = "Join as a partner";
            return View(new RegisterPartnerViewModel
            {
                Categories = await _categories.GetActiveOptionsAsync(ct),
            });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [TrackForm(KycHistoryService.FormType)]
        public async Task<IActionResult> Register(RegisterPartnerViewModel model, CancellationToken ct)
        {
            ViewData["Title"] = "Join as a partner";

            // Repopulate before any return — the picker is lost on a round trip otherwise.
            model.Categories = await _categories.GetActiveOptionsAsync(ct);

            if (!ModelState.IsValid) return View(model);

            AuthResult? outcome = null;

            var failed = await SignInAsync(
                async () => outcome = await AuthService.RegisterPartnerAsync(new RegisterPartnerRequest
                {
                    Name = model.Name,
                    Phone = model.Phone,
                    Email = model.Email,
                    Password = model.Password,
                    SkillCategoryId = model.SkillCategoryId,
                    Selfie = model.Selfie,
                    AadhaarFront = model.AadhaarFront,
                    AadhaarBack = model.AadhaarBack,
                    AadhaarNumber = model.AadhaarNumber,
                }),
                nameof(Register), model);

            if (failed is not null) return failed;

            var profile = outcome?.Response?.User.PartnerProfile;
            if (profile is not null) TrackDoc(profile.Id, profile);

            TempData["Success"] = "Account created. An admin will review your KYC before you can accept work.";
            return Redirect(DashboardPath);
        }

        [HttpGet("")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewData["Title"] = "Partner dashboard";

            var userId = User.GetRequiredUserId();

            var partner = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .FirstOrDefaultAsync(p => p.UserId == userId, ct);

            var model = new ProviderDashboardViewModel
            {
                Name = User.Identity?.Name ?? "there",
                Profile = partner is null ? null : PartnerDto.From(partner),
                Categories = await _categories.GetOptionsIncludingAsync(partner?.SkillCategoryId, ct),
            };

            if (partner is not null)
            {
                // An unverified partner keeps the jobs they already hold, so a
                // skill change cannot strand a customer mid-booking. Everything
                // else on the dashboard stays hidden until they are approved.
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
                model.MyJobs = myJobs.Select(GigTaskDto.From).ToList();

                if (partner.IsVerified)
                {
                    model.MyBids = await _bids.ForPartnerAsync(userId, ct);
                    // Already-bid tasks move to "My bids", so drop them here to
                    // avoid the partner thinking they still need to act.
                    var alreadyBidTaskIds = model.MyBids
                        .Where(b => b.IsOpen || b.Status == BidStatus.Accepted)
                        .Select(b => b.GigTaskId)
                        .ToHashSet();

                    var open = _context.GigTasks
                        .AsNoTracking()
                        .Include(t => t.Customer)
                        .Include(t => t.Category)
                        .Include(t => t.ServiceItem)
                        .Where(t => t.Status == GigTaskStatus.Pending
                                 && t.CategoryId == partner.SkillCategoryId);

                    // Work outside the radius the partner set is not work they
                    // will take, so PostGIS drops it before it is ever loaded.
                    // A task with no pin stays in — an old booking without one
                    // is still worth doing.
                    if (partner.BaseLocation is not null)
                    {
                        var radiusMetres = partner.ServiceRadiusKm * GeoPoint.MetresPerKm;

                        open = open.Where(t => t.Location == null
                                            || t.Location.Distance(partner.BaseLocation) <= radiusMetres);
                    }

                    // Urgent work first, then newest — a same-day job is no use
                    // to anyone sitting three pages down.
                    var available = await open
                        .OrderBy(t => t.Urgency == TaskUrgency.Urgent ? 0
                                    : t.Urgency == TaskUrgency.Normal ? 1 : 2)
                        .ThenByDescending(t => t.CreatedAt)
                        .ToListAsync(ct);

                    var shortlist = available
                        .Where(t => !alreadyBidTaskIds.Contains(t.Id))
                        .ToList();

                    var distances = await _match.DistancesFromPartnerAsync(
                        partner.Id, shortlist.Select(t => t.Id), ct);

                    model.AvailableTasks = shortlist
                        .Select(t =>
                        {
                            var dto = GigTaskDto.From(t);
                            dto.DistanceKm = distances.TryGetValue(t.Id, out var km) ? km : null;
                            return dto;
                        })
                        .OrderBy(t => t.Urgency == TaskUrgency.Urgent ? 0
                                    : t.Urgency == TaskUrgency.Normal ? 1 : 2)
                        .ThenBy(t => t.DistanceKm ?? double.MaxValue)
                        .ThenByDescending(t => t.CreatedAt)
                        .ToList();

                    model.HasServiceArea = partner.BaseLocation is not null;
                    model.ServiceRadiusKm = partner.ServiceRadiusKm;
                }
            }

            return View(model);
        }

        // Partners no longer claim a task directly — they bid, and the customer
        // decides. The task only becomes theirs when a bid is accepted.
        [HttpPost("tasks/{id:int}/bid")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Bid")]
        public async Task<IActionResult> PlaceBid(int id, PlaceBidRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BidError(FirstModelError());

            var result = await _bids.PlaceAsync(User.GetRequiredUserId(), id, form, ct);
            if (!result.Succeeded) return BidError(result.Error);

            TrackDoc(result.Bid!.Id, result.Bid);
            TempData["Success"] = $"Bid of ₹{form.Amount:N0} placed on task #{id}.";
            return Redirect(DashboardPath);
        }

        [HttpPost("tasks/{id:int}/accept")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("InstantAccept")]
        public async Task<IActionResult> AcceptTask(int id, CancellationToken ct)
        {
            var result = await _claims.ClaimAsync(User.GetRequiredUserId(), id, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Error;
                return Redirect(DashboardPath);
            }

            TempData["Success"] =
                $"Job #{id} is yours at ₹{result.Amount:N0}. It is now under My jobs.";

            return Redirect(DashboardPath);
        }

        [HttpPost("bids/{bidId:int}/withdraw")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Bid")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> WithdrawBid(int bidId, CancellationToken ct)
        {
            var result = await _bids.WithdrawAsync(User.GetRequiredUserId(), bidId, ct);
            if (!result.Succeeded) return BidError(result.Error);

            TrackDoc(bidId, result.Bid);
            TempData["Success"] = "Bid withdrawn.";
            return Redirect(DashboardPath);
        }

        [HttpPost("bids/{bidId:int}/accept-counter")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        [TrackForm("Bid")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> AcceptCounter(int bidId, CancellationToken ct)
        {
            var result = await _bids.AcceptCounterAsync(User.GetRequiredUserId(), bidId, ct);
            if (!result.Succeeded) return BidError(result.Error);

            TrackDoc(bidId, result.Bid);
            TempData["Success"] =
                $"Counter offer accepted at ₹{result.Bid!.CurrentAmount:N0}. The job is now yours.";

            return Redirect(DashboardPath);
        }

        private IActionResult BidError(string? message)
        {
            TempData["Error"] = message ?? "Could not save your bid.";
            return Redirect(DashboardPath);
        }

        private string? FirstModelError() => ModelState
            .SelectMany(e => e.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

        [HttpPost("tasks/{id:int}/status")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int id, string status, int stars, string? feedback, CancellationToken ct)
        {
            var partner = await GetOwnPartnerAsync(ct);
            var task = partner is null
                ? null
                : await _context.GigTasks
                    .Include(t => t.Partner)
                    .FirstOrDefaultAsync(t => t.Id == id && t.PartnerId == partner.Id, ct);

            if (task is null)
            {
                TempData["Error"] = "Task not found.";
                return Redirect(DashboardPath);
            }

            if (!GigTaskStatus.IsValid(status) || !GigTaskStatus.CanTransition(task.Status, status))
            {
                TempData["Error"] = $"Cannot move task #{id} to '{status}'.";
                return Redirect(DashboardPath);
            }

            var isCompleting = status == GigTaskStatus.Completed;

            if (isCompleting && !RatingScale.IsValid(stars))
            {
                TempData["Error"] = "Rate the customer before you close this job.";
                return Redirect(DashboardPath);
            }

            if (isCompleting)
            {
                var rating = await Ratings.BuildAsync(
                    User.GetRequiredUserId(), RatedBy.Partner, task, stars, feedback, ct);

                if (rating is not null) _context.TaskRatings.Add(rating);
            }

            task.Status = status;
            task.CompletedAt = isCompleting ? DateTime.UtcNow : null;
            await _context.SaveChangesAsync(ct);

            if (isCompleting)
            {
                await _earnings.PostJobEarningAsync(task, ct);
                await Ratings.RefreshAverageAsync(task.CustomerId, ct);
            }

            TrackDoc(task.Id, GigTaskDto.From(task));
            TempData["Success"] = isCompleting
                ? $"Task #{id} is complete. Your earning has been added to your balance."
                : $"Task #{id} is now {status.Replace('_', ' ')}.";

            return Redirect(DashboardPath);
        }

        [HttpGet("earnings")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public IActionResult Earnings() => Redirect($"{ProfilePath}/{ProfileSections.Earnings}");

        [HttpPost("availability")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAvailability(bool isAvailable, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "That request could not be read. Reload the page and try again.";
                return Redirect(DashboardPath);
            }

            var partner = await GetOwnPartnerAsync(ct);

            if (partner is not null)
            {
                partner.IsAvailable = isAvailable;
                await _context.SaveChangesAsync(ct);
                TempData["Success"] = isAvailable ? "You are now accepting work." : "You are now off duty.";
            }

            return Redirect(DashboardPath);
        }

        [HttpPost("skill")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [TrackForm(KycHistoryService.FormType)]
        [TrackEntry(TrackingEntryType.Update)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSkill(int skillCategoryId, CancellationToken ct)
        {
            var partner = await GetOwnPartnerAsync(ct);

            if (partner is null)
            {
                TempData["Error"] = "No partner profile is attached to this account.";
            }
            else if (!await _categories.IsSelectableAsync(skillCategoryId, ct))
            {
                TempData["Error"] = "Choose a valid skill category.";
            }
            else
            {
                var fromName = await _categories.GetNameAsync(partner.SkillCategoryId, ct);
                var toName = await _categories.GetNameAsync(skillCategoryId, ct);

                var sentForReview = PartnerKyc.ChangeSkill(partner, skillCategoryId, fromName, toName);
                await _context.SaveChangesAsync(ct);

                TrackDoc(partner.Id, PartnerDto.From(partner));
                TempData["Success"] = sentForReview
                    ? $"Skill changed to {toName}. Your KYC has gone back for approval, so you cannot accept work until an administrator approves it."
                    : "Skill category updated.";
            }

            return Redirect(KycPath);
        }

        [HttpPost("kyc")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [TrackForm(KycHistoryService.FormType)]
        [TrackEntry(TrackingEntryType.Update)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitKyc(
            [FromForm] UpdateKycRequest request, CancellationToken ct)
        {
            var partner = await GetOwnPartnerAsync(ct);

            if (partner is null)
            {
                TempData["Error"] = "No partner profile is attached to this account.";
                return Redirect(KycPath);
            }

            if (!ModelState.IsValid || !request.HasAnything)
            {
                TempData["Error"] = ModelState.IsValid
                    ? "Choose at least one document to upload."
                    : string.Join(" ", ModelState.SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage));

                return Redirect(KycPath);
            }

            var replaced = new List<string>();

            var selfie = await ReplaceKycAsync(request.Selfie, partner.SelfieFileName, replaced, ct);
            var front = await ReplaceKycAsync(request.AadhaarFront, partner.AadhaarFrontFileName, replaced, ct);
            var back = await ReplaceKycAsync(request.AadhaarBack, partner.AadhaarBackFileName, replaced, ct);

            var error = selfie.Error ?? front.Error ?? back.Error;
            if (error is not null)
            {
                TempData["Error"] = error;
                return Redirect(KycPath);
            }

            partner.SelfieFileName = selfie.FileName;
            partner.AadhaarFrontFileName = front.FileName;
            partner.AadhaarBackFileName = back.FileName;

            if (!string.IsNullOrWhiteSpace(request.AadhaarNumber))
                partner.AadhaarNumber = request.AadhaarNumber.Trim();

            PartnerKyc.SubmitDocuments(partner);
            await _context.SaveChangesAsync(ct);

            foreach (var old in replaced) _storage.Delete(old, FileCategory.KycDocument);

            TrackDoc(partner.Id, PartnerDto.From(partner));
            TempData["Success"] = "Documents uploaded. An admin will review them shortly.";

            return Redirect(KycPath);
        }

        /// <summary>Stores a replacement if one was sent, otherwise keeps the current name.</summary>
        private async Task<(string? FileName, string? Error)> ReplaceKycAsync(
            IFormFile? file, string? current, List<string> replaced, CancellationToken ct)
        {
            if (file is null) return (current, null);

            var saved = await _storage.SaveAsync(file, FileCategory.KycDocument, ct);
            if (!saved.Succeeded) return (current, saved.Error);

            if (!string.IsNullOrWhiteSpace(current)) replaced.Add(current);

            return (saved.FileName, null);
        }

        [HttpPost("logout")]
        [SkipTracking]   // signing out changes no data
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            ClearAuthCookie();
            return Redirect(LoginPath);
        }

        private Task<Partner?> GetOwnPartnerAsync(CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();
            return _context.Partners.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        }
    }
}
