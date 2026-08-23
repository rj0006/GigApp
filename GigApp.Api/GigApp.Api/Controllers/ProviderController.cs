using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Bidding;
using GigApp.Api.Services.Files;
using GigApp.Api.Services.Profile;
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

        public ProviderController(
            IAuthService authService,
            IProfileService profileService,
            AppDbContext context,
            ICategoryLookup categories,
            IFileStorageService storage,
            IBidService bids)
            : base(authService, profileService)
        {
            _context = context;
            _categories = categories;
            _storage = storage;
            _bids = bids;
        }

        protected override string PortalSlug => "provider";
        protected override string RequiredRole => UserRoles.Partner;

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
        public async Task<IActionResult> Register(RegisterPartnerViewModel model, CancellationToken ct)
        {
            ViewData["Title"] = "Join as a partner";

            // Repopulate before any return — the picker is lost on a round trip otherwise.
            model.Categories = await _categories.GetActiveOptionsAsync(ct);

            if (!ModelState.IsValid) return View(model);

            var failed = await SignInAsync(
                () => AuthService.RegisterPartnerAsync(new RegisterPartnerRequest
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
                // Only assigned work lands here — a bid on its own does not.
                var myJobs = await _context.GigTasks
                    .AsNoTracking()
                    .Include(t => t.Customer)
                    .Include(t => t.Category)
                    .Include(t => t.ServiceItem)
                    .Where(t => t.PartnerId == partner.Id)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(ct);

                model.MyJobs = myJobs.Select(GigTaskDto.From).ToList();
                model.MyBids = await _bids.ForPartnerAsync(userId, ct);

                // Unverified partners see an empty board — the API enforces the
                // same rule, this just avoids showing work they cannot take.
                if (partner.IsVerified)
                {
                    // Already-bid tasks move to "My bids", so drop them here to
                    // avoid the partner thinking they still need to act.
                    var alreadyBidTaskIds = model.MyBids
                        .Where(b => b.IsOpen || b.Status == BidStatus.Accepted)
                        .Select(b => b.GigTaskId)
                        .ToHashSet();

                    // Urgent work first, then newest — a same-day job is no use
                    // to anyone sitting three pages down.
                    var available = await _context.GigTasks
                        .AsNoTracking()
                        .Include(t => t.Customer)
                        .Include(t => t.Category)
                        .Include(t => t.ServiceItem)
                        .Where(t => t.Status == GigTaskStatus.Pending
                                 && t.CategoryId == partner.SkillCategoryId)
                        .OrderBy(t => t.Urgency == TaskUrgency.Urgent ? 0
                                    : t.Urgency == TaskUrgency.Normal ? 1 : 2)
                        .ThenByDescending(t => t.CreatedAt)
                        .ToListAsync(ct);

                    model.AvailableTasks = available
                        .Where(t => !alreadyBidTaskIds.Contains(t.Id))
                        .Select(GigTaskDto.From)
                        .ToList();
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
        public async Task<IActionResult> UpdateStatus(int id, string status, CancellationToken ct)
        {
            var partner = await GetOwnPartnerAsync(ct);
            var task = partner is null
                ? null
                : await _context.GigTasks.FirstOrDefaultAsync(t => t.Id == id && t.PartnerId == partner.Id, ct);

            if (task is null)
            {
                TempData["Error"] = "Task not found.";
            }
            else if (!GigTaskStatus.IsValid(status) || !GigTaskStatus.CanTransition(task.Status, status))
            {
                TempData["Error"] = $"Cannot move task #{id} to '{status}'.";
            }
            else
            {
                task.Status = status;
                task.CompletedAt = status == GigTaskStatus.Completed ? DateTime.UtcNow : null;
                await _context.SaveChangesAsync(ct);
                TempData["Success"] = $"Task #{id} is now {status.Replace('_', ' ')}.";
            }

            return Redirect(DashboardPath);
        }

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
                partner.SkillCategoryId = skillCategoryId;
                await _context.SaveChangesAsync(ct);
                TempData["Success"] = "Skill category updated.";
            }

            return Redirect(DashboardPath);
        }

        [HttpPost("kyc")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitKyc(
            [FromForm] UpdateKycRequest request, CancellationToken ct)
        {
            var partner = await GetOwnPartnerAsync(ct);

            if (partner is null)
            {
                TempData["Error"] = "No partner profile is attached to this account.";
                return Redirect(DashboardPath);
            }

            if (!ModelState.IsValid || !request.HasAnything)
            {
                TempData["Error"] = ModelState.IsValid
                    ? "Choose at least one document to upload."
                    : string.Join(" ", ModelState.SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage));

                return Redirect(DashboardPath);
            }

            var replaced = new List<string>();

            var selfie = await ReplaceKycAsync(request.Selfie, partner.SelfieFileName, replaced, ct);
            var front = await ReplaceKycAsync(request.AadhaarFront, partner.AadhaarFrontFileName, replaced, ct);
            var back = await ReplaceKycAsync(request.AadhaarBack, partner.AadhaarBackFileName, replaced, ct);

            var error = selfie.Error ?? front.Error ?? back.Error;
            if (error is not null)
            {
                TempData["Error"] = error;
                return Redirect(DashboardPath);
            }

            partner.SelfieFileName = selfie.FileName;
            partner.AadhaarFrontFileName = front.FileName;
            partner.AadhaarBackFileName = back.FileName;

            if (!string.IsNullOrWhiteSpace(request.AadhaarNumber))
                partner.AadhaarNumber = request.AadhaarNumber.Trim();

            partner.IsVerified = false;   // re-submitting sends it back through review
            await _context.SaveChangesAsync(ct);

            foreach (var old in replaced) _storage.Delete(old, FileCategory.KycDocument);

            TrackDoc(partner.Id, PartnerDto.From(partner));
            TempData["Success"] = "Documents uploaded. An admin will review them shortly.";

            return Redirect(DashboardPath);
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
