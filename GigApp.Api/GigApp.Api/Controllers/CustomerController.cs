using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Storefront;
using GigApp.Api.Services.Otp;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Notifications;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Tracking;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GigApp.Api.Controllers
{
    [Route("customer")]
    public class CustomerController : PortalControllerBase
    {
        private readonly ICartService _cart;
        private readonly IOtpService _otp;
        private readonly IAuthSettingsService _authSettings;

        public CustomerController(
            IAuthService authService,
            IRefreshTokenService refreshTokens,
            IProfileService profileService,
            ISupportService support,
            INotificationService notifier,
            ICartService cart,
            IOtpService otp,
            IAuthSettingsService authSettings)
            : base(authService, refreshTokens, profileService, support, notifier)
        {
            _cart = cart;
            _otp = otp;
            _authSettings = authSettings;
        }

        protected override string PortalSlug => "customer";
        protected override string RequiredRole => UserRoles.Customer;

        // The storefront at /services is the one customer home page — this
        // portal no longer has a separate dashboard to send anyone to.
        protected override string DashboardPath => "/services";

        [HttpGet("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl, bool denied, string? mode, CancellationToken ct)
        {
            // Already signed in as a customer — no reason to show the form again.
            if (IsAlreadySignedIn) return RedirectToLocalOr(returnUrl);

            ViewData["Title"] = "Customer sign in";

            var settings = await _authSettings.GetAsync(ct);

            if (settings.CustomerLoginMode == LoginMode.Otp && mode != LoginMode.Password)
            {
                return View("LoginOtp", new OtpAuthViewModel
                {
                    PortalSlug = PortalSlug,
                    PortalLabel = "Customer",
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
            var otpAuth = result.Auth;

            if (!result.Succeeded || otpAuth is null)
                return BadRequest(new ProblemDetails { Title = result.Error ?? "Could not verify that code.", Status = 400 });

            var failed = await SignInJsonAsync(
                () => Task.FromResult(otpAuth),
                auth => _cart.MergeIntoAccountAsync(auth.User.Id, ct));

            if (failed is not null) return failed;

            TempData["Success"] = "Signed in.";
            return Ok(new { redirectTo = LocalRedirectTarget(form.ReturnUrl) });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [SkipTracking]   // signing in changes no data
        [EnableRateLimiting(RateLimiterPolicies.Auth)]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ProblemDetails { Title = FirstError() ?? "Enter your details and try again.", Status = 400 });

            var failed = await SignInJsonAsync(
                () => AuthService.LoginAsync(new LoginRequest
                {
                    Identifier = model.Identifier,
                    Password = model.Password,
                    Role = RequiredRole,
                }),
                auth => _cart.MergeIntoAccountAsync(auth.User.Id, ct));

            if (failed is not null) return failed;

            return Ok(new { redirectTo = LocalRedirectTarget(model.ReturnUrl) });
        }

        [HttpGet("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(CancellationToken ct)
        {
            if (IsAlreadySignedIn) return Redirect(DashboardPath);

            var settings = await _authSettings.GetAsync(ct);

            // A new account is just an unrecognised number on the OTP flow —
            // there is no separate registration screen for it to go to.
            if (settings.CustomerLoginMode == LoginMode.Otp) return Redirect("/customer/login");

            ViewData["Title"] = "Create a customer account";
            return View(new RegisterCustomerViewModel());
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimiterPolicies.Auth)]
        public async Task<IActionResult> Register(RegisterCustomerViewModel model, CancellationToken ct)
        {
            ViewData["Title"] = "Create a customer account";
            if (!ModelState.IsValid) return View(model);

            var failed = await SignInAsync(
                () => AuthService.RegisterCustomerAsync(new RegisterCustomerRequest
                {
                    Name = model.Name,
                    Phone = model.Phone,
                    Email = model.Email,
                    Password = model.Password,
                }),
                nameof(Register), model,
                auth => _cart.MergeIntoAccountAsync(auth.User.Id, ct));

            if (failed is not null) return failed;

            TempData["Success"] = "Welcome to GigApp. Your account is ready.";
            return Redirect(DashboardPath);
        }

        // The storefront at /services is the single customer home page now —
        // this route is kept only so old links and bookmarks still land somewhere.
        [HttpGet("")]
        [Authorize(Policy = Policies.CustomerOnly)]
        public IActionResult Index() => Redirect("/services");

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
