using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Otp;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Notifications;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Tracking;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("provider")]
    public class ProviderController : PortalControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICategoryLookup _categories;
        private readonly IOtpService _otp;
        private readonly IAuthSettingsService _authSettings;

        public ProviderController(
            IAuthService authService,
            IRefreshTokenService refreshTokens,
            IProfileService profileService,
            AppDbContext context,
            ICategoryLookup categories,
            ISupportService support,
            INotificationService notifier,
            IOtpService otp,
            IAuthSettingsService authSettings)
            : base(authService, refreshTokens, profileService, support, notifier)
        {
            _context = context;
            _categories = categories;
            _otp = otp;
            _authSettings = authSettings;
        }

        protected override string PortalSlug => "provider";
        protected override string RequiredRole => UserRoles.Partner;

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

        protected override async Task<ProfileExtras> LoadProfileExtrasAsync(
            string section, CancellationToken ct)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .FirstOrDefaultAsync(p => p.UserId == User.GetRequiredUserId(), ct);

            return new ProfileExtras { Partner = partner is null ? null : PartnerDto.From(partner) };
        }

        [HttpGet("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl, bool denied, string? mode, CancellationToken ct)
        {
            // Already signed in as a partner — no reason to show the form again.
            if (IsAlreadySignedIn) return RedirectToLocalOr(returnUrl);

            ViewData["Title"] = "Partner sign in";

            var settings = await _authSettings.GetAsync(ct);

            if (settings.PartnerLoginMode == LoginMode.Otp && mode != LoginMode.Password)
            {
                return View("LoginOtp", new OtpAuthViewModel
                {
                    PortalSlug = PortalSlug,
                    PortalLabel = "Partner",
                    ReturnUrl = returnUrl,
                });
            }

            return View(BuildLoginModel(returnUrl, denied));
        }

        [HttpPost("otp/request")]
        [AllowAnonymous]
        [SkipTracking]   // requesting a code changes no data
        [EnableRateLimiting(RateLimiterPolicies.OtpRequest)]
        public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ProblemDetails { Title = FirstError() ?? "Enter a valid mobile number.", Status = 400 });

            var result = await _otp.RequestAsync(form.Phone, RequiredRole, ct);

            if (!result.Succeeded)
                return BadRequest(new ProblemDetails { Title = result.Error ?? "Could not send the code.", Status = 400 });

            return Ok(new { devCode = result.DevCode });
        }

        [HttpPost("otp/verify")]
        [AllowAnonymous]
        [SkipTracking]   // signing in changes no data
        [EnableRateLimiting(RateLimiterPolicies.Auth)]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ProblemDetails { Title = "Enter the 6-digit code.", Status = 400 });

            var result = await _otp.VerifyAsync(form.Phone, RequiredRole, form.Code, form.Name, ct);

            if (result.RequiresPartnerRegistration)
            {
                TempData["Success"] = "Number verified. Finish your KYC to complete your account.";
                return Ok(new { redirectTo = $"/provider/register?phone={Uri.EscapeDataString(form.Phone)}&verified=true" });
            }

            var otpAuth = result.Auth;

            if (!result.Succeeded || otpAuth is null)
                return BadRequest(new ProblemDetails { Title = result.Error ?? "Could not verify that code.", Status = 400 });

            var failed = await SignInJsonAsync(() => Task.FromResult(otpAuth));
            if (failed is not null) return failed;

            TempData["Success"] = "Signed in.";
            return Ok(new { redirectTo = LocalRedirectTarget(form.ReturnUrl) });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [SkipTracking]   // signing in changes no data
        [EnableRateLimiting(RateLimiterPolicies.Auth)]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ProblemDetails { Title = FirstError() ?? "Enter your details and try again.", Status = 400 });

            var failed = await SignInJsonAsync(() => AuthService.LoginAsync(new LoginRequest
            {
                Identifier = model.Identifier,
                Password = model.Password,
                Role = RequiredRole,
            }));

            if (failed is not null) return failed;

            return Ok(new { redirectTo = LocalRedirectTarget(model.ReturnUrl) });
        }

        [HttpGet("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(CancellationToken ct)
        {
            if (IsAlreadySignedIn) return Redirect(DashboardPath);

            ViewData["Title"] = "Join as a partner";

            return View(new RegisterPartnerShellViewModel
            {
                Categories = await _categories.GetActiveOptionsAsync(ct),
            });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [TrackForm(KycHistoryService.FormType)]
        [EnableRateLimiting(RateLimiterPolicies.Auth)]
        public async Task<IActionResult> Register(
            RegisterPartnerRequest request, [FromForm] bool phoneVerifiedViaOtp, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ProblemDetails { Title = FirstError() ?? "Check the form and try again.", Status = 400 });

            AuthResult? outcome = null;

            var failed = await SignInJsonAsync(async () => outcome = await AuthService.RegisterPartnerAsync(request));
            if (failed is not null) return failed;

            if (phoneVerifiedViaOtp && outcome?.Response is not null)
            {
                await _context.Users
                    .Where(u => u.Id == outcome.Response.User.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.IsPhoneVerified, true)
                        .SetProperty(u => u.PhoneVerifiedAt, DateTime.UtcNow), ct);
            }

            var profile = outcome?.Response?.User.PartnerProfile;
            if (profile is not null) TrackDoc(profile.Id, profile);

            TempData["Success"] = "Account created. An admin will review your KYC before you can accept work.";
            return Ok(new { redirectTo = DashboardPath });
        }

        [HttpGet("")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public IActionResult Index()
        {
            ViewData["Title"] = "Partner dashboard";
            return View();
        }

        [HttpGet("earnings")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public IActionResult Earnings() => Redirect($"{ProfilePath}/{ProfileSections.Earnings}");

        [HttpPost("logout")]
        [SkipTracking]   // signing out changes no data
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            await ClearAuthCookieAsync(ct);
            return Redirect(LoginPath);
        }

    }
}
