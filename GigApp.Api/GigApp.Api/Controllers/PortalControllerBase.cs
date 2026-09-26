using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Notifications;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Support;
using GigApp.Api.Services.Tracking;
using GigApp.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// Shared plumbing for the three server-rendered portals. Each portal signs
    /// in through the same <see cref="IAuthService"/> the mobile apps use — the
    /// only difference is that the JWT is parked in an HttpOnly cookie instead
    /// of being handed to the caller.
    /// </summary>
    public abstract class PortalControllerBase : Controller
    {
        protected readonly IAuthService AuthService;
        protected readonly IRefreshTokenService RefreshTokens;
        protected readonly IProfileService ProfileService;
        protected readonly ISupportService Support;
        protected readonly INotificationService Notifier;

        protected PortalControllerBase(
            IAuthService authService,
            IRefreshTokenService refreshTokens,
            IProfileService profileService,
            ISupportService support,
            INotificationService notifier)
        {
            AuthService = authService;
            RefreshTokens = refreshTokens;
            ProfileService = profileService;
            Support = support;
            Notifier = notifier;
        }

        protected string ProfilePath => $"/{PortalSlug}/profile";
        protected string AddressesPath => $"{ProfilePath}/{ProfileSections.Addresses}";

        /// <summary>
        /// Every portal view needs its own home link, so the navbar brand points
        /// at the dashboard rather than the public landing page.
        /// </summary>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            ViewData["PortalHome"] = User.Identity?.IsAuthenticated == true && PrincipalAccepted
                ? DashboardPath
                : "/";

            base.OnActionExecuting(context);
        }

        // The bell is on every page of every portal, so it is filled once here
        // rather than in each action.
        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (User.Identity?.IsAuthenticated == true && PrincipalAccepted)
            {
                var userId = User.GetUserId();

                if (userId is not null)
                {
                    var ct = context.HttpContext.RequestAborted;
                    ViewData["UnreadCount"] = await Notifier.UnreadCountAsync(userId.Value, ct);
                    ViewData["RecentNotifications"] = await Notifier.RecentAsync(userId.Value, 6, ct);
                }
            }

            await base.OnActionExecutionAsync(context, next);
        }

        /// <summary>Route prefix and role this portal serves, e.g. "customer".</summary>
        protected abstract string PortalSlug { get; }
        protected abstract string RequiredRole { get; }

        /// <summary>
        /// Whether a role may use this portal. Exact match by default; the admin
        /// portal widens it so a super admin is not locked out of the door they
        /// have more rights behind.
        /// </summary>
        protected virtual bool AcceptsRole(string? role) =>
            string.Equals(role, RequiredRole, StringComparison.Ordinal);

        /// <summary>Same question, asked of the signed-in principal.</summary>
        protected virtual bool PrincipalAccepted => User.IsInRole(RequiredRole);

        protected string LoginPath => $"/{PortalSlug}/login";
        protected virtual string DashboardPath => $"/{PortalSlug}";

        // Declared once here so /admin/profile, /provider/profile and
        // /customer/profile all work from a single implementation — attribute
        // routes on a base action are picked up under each derived prefix.
        // Every one of these calls IProfileService, the same service the API
        // uses, so the rules cannot drift between web and mobile.

        [HttpGet("profile")]
        [Authorize]
        public Task<IActionResult> Profile(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Details, ct);

        [HttpGet("profile/bank")]
        [Authorize]
        public Task<IActionResult> ProfileBank(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Bank, ct);

        [HttpGet("profile/addresses")]
        [Authorize]
        public Task<IActionResult> ProfileAddresses(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Addresses, ct);

        [HttpGet("notifications")]
        [Authorize]
        public async Task<IActionResult> Notifications(
            [FromQuery] PageRequest paging, CancellationToken ct)
        {
            ViewData["Title"] = "Notifications";

            var userId = User.GetRequiredUserId();
            var list = await Notifier.ListAsync(userId, paging, ct);

            // Opening the page is reading them; a badge that survives the click
            // is just noise on every later page.
            await Notifier.MarkReadAsync(userId, null, ct);

            return View("Notifications", new NotificationsViewModel
            {
                Notifications = list,
                PortalSlug = PortalSlug,
            });
        }

        [HttpGet("profile/orders")]
        [Authorize]
        public Task<IActionResult> ProfileOrders(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Orders, ct);

        [HttpGet("profile/post")]
        [Authorize]
        public Task<IActionResult> ProfilePostTask(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.PostTask, ct);

        [HttpGet("profile/tasks")]
        [Authorize]
        public Task<IActionResult> ProfileTasks(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Tasks, ct);

        [HttpGet("profile/settings")]
        [Authorize]
        public Task<IActionResult> ProfileSettings(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Settings, ct);

        [HttpGet("profile/devices")]
        [Authorize]
        public Task<IActionResult> ProfileDevices(CancellationToken ct) =>
            ProfileSectionAsync(ProfileSections.Devices, ct);

        protected virtual Task<ProfileExtras> LoadProfileExtrasAsync(
            string section, CancellationToken ct) => Task.FromResult(new ProfileExtras());

        private static readonly string[] CustomerOnlySections =
            { ProfileSections.PostTask, ProfileSections.Tasks };

        private static readonly string[] PartnerOnlySections =
            { ProfileSections.ServiceArea };

        private static readonly string[] BookingSections =
            { ProfileSections.Bank, ProfileSections.Addresses, ProfileSections.Orders };

        // An administrator is never paid and never booked, so a bank account, a
        // delivery address and an order history mean nothing on their account.
        private static bool SectionApplies(string section, string role) =>
            (!UserRoles.IsAdminRole(role) || !BookingSections.Contains(section))
            && (role == UserRoles.Customer || !CustomerOnlySections.Contains(section))
            && (role == UserRoles.Partner || !PartnerOnlySections.Contains(section));

        protected async Task<IActionResult> ProfileSectionAsync(string section, CancellationToken ct)
        {
            ViewData["Title"] = "My profile";

            var userId = User.GetRequiredUserId();
            var user = await ProfileService.GetAsync(userId, ct);

            if (user is null) return Redirect(LoginPath);

            if (!SectionApplies(section, user.Role)) return Redirect(ProfilePath);

            var extras = await LoadProfileExtrasAsync(section, ct);

            return View("Profile", new ProfilePageViewModel
            {
                User = user,
                Section = section,
                PortalSlug = PortalSlug,
                Partner = extras.Partner,
            });
        }

        // Declared here for the same reason as profile: one implementation,
        // reachable as /customer/addresses and /provider/addresses.

        [HttpGet("addresses")]
        [Authorize]
        public IActionResult Addresses() => Redirect(AddressesPath);

        protected string? FirstError() => ModelState
            .SelectMany(e => e.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

        /// <summary>
        /// Runs a login/registration attempt and, on success, issues the cookie.
        /// Returns null when it worked; otherwise the view to re-render.
        /// </summary>
        protected async Task<IActionResult?> SignInAsync(
            Func<Task<AuthResult>> attempt, string viewName, object model,
            Func<AuthResponse, Task>? onSuccess = null)
        {
            var result = await attempt();

            if (!result.Succeeded || result.Response is null)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Something went wrong.");
                return View(viewName, model);
            }

            // Stop a customer from signing in through the admin door.
            if (!AcceptsRole(result.Response.User.Role))
            {
                ModelState.AddModelError(string.Empty,
                    $"This is the {PortalSlug} portal. Your account is registered as a {result.Response.User.Role}.");
                return View(viewName, model);
            }

            IssueAuthCookie(result.Response);
            if (onSuccess is not null) await onSuccess(result.Response);
            return null;
        }

        // Same contract as SignInAsync, for a page that submits over fetch: null means the cookie is issued.
        protected async Task<IActionResult?> SignInJsonAsync(
            Func<Task<AuthResult>> attempt, Func<AuthResponse, Task>? onSuccess = null)
        {
            var result = await attempt();

            if (!result.Succeeded || result.Response is null)
                return BadRequest(new ProblemDetails { Title = result.Error ?? "Something went wrong.", Status = 400 });

            if (!AcceptsRole(result.Response.User.Role))
                return BadRequest(new ProblemDetails
                {
                    Title = $"This is the {PortalSlug} portal. Your account is registered as a {result.Response.User.Role}.",
                    Status = 400,
                });

            IssueAuthCookie(result.Response);
            if (onSuccess is not null) await onSuccess(result.Response);
            return null;
        }

        protected void IssueAuthCookie(AuthResponse auth)
        {
            Response.Cookies.Append(AuthCookie.Name, auth.Token, new CookieOptions
            {
                HttpOnly = true,                       // script must never read the token
                Secure = Request.IsHttps,              // dev runs plain HTTP; do not drop the cookie there
                SameSite = SameSiteMode.Lax,
                Expires = auth.ExpiresAtUtc,
                Path = "/",
            });

            Response.Cookies.Append(AuthCookie.RefreshName, auth.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = auth.RefreshExpiresAtUtc,
                Path = "/",
            });
        }

        // Revokes the refresh token server-side, not just the cookies — a copy
        // an attacker captured earlier must stop working the moment the real
        // owner signs out, not linger until it happens to expire.
        protected async Task ClearAuthCookieAsync(CancellationToken ct)
        {
            Request.Cookies.TryGetValue(AuthCookie.RefreshName, out var refreshToken);
            await RefreshTokens.RevokeByRawTokenAsync(refreshToken, ct);

            Response.Cookies.Delete(AuthCookie.Name);
            Response.Cookies.Delete(AuthCookie.RefreshName);
        }

        // Silent re-auth for the browser: called periodically by global.js so a
        // portal session outlives the short-lived access token without ever
        // showing the user a login screen, as long as the refresh token (30
        // days of activity) is still good.
        [HttpPost("refresh-session")]
        [AllowAnonymous]
        [SkipTracking]
        public async Task<IActionResult> RefreshSession(CancellationToken ct)
        {
            if (!Request.Cookies.TryGetValue(AuthCookie.RefreshName, out var refreshToken)
                || string.IsNullOrEmpty(refreshToken))
                return Unauthorized();

            var deviceLabel = DeviceLabel.FromUserAgent(Request.Headers.UserAgent.ToString());
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await RefreshTokens.RedeemAsync(refreshToken, deviceLabel, ipAddress, ct);

            if (!result.Succeeded || result.Response is null)
            {
                Response.Cookies.Delete(AuthCookie.Name);
                Response.Cookies.Delete(AuthCookie.RefreshName);
                return Unauthorized();
            }

            IssueAuthCookie(result.Response);
            return Ok();
        }

        /// <summary>
        /// Hands the saved record to the audit filter. Portal actions redirect,
        /// so unlike the API there is no returned DTO for the filter to read —
        /// call this after a successful save.
        /// </summary>
        protected void TrackDoc(object? docNo, object? result = null) =>
            HttpContext.TrackDoc(docNo, result);

        /// <summary>
        /// Only follow a return URL that stays on this site, and never one that
        /// points back at the login page — that would loop forever once the
        /// caller is already signed in.
        /// </summary>
        protected IActionResult RedirectToLocalOr(string? returnUrl) => Redirect(LocalRedirectTarget(returnUrl));

        protected string LocalRedirectTarget(string? returnUrl)
        {
            var isUsable = !string.IsNullOrEmpty(returnUrl)
                && Url.IsLocalUrl(returnUrl)
                && !returnUrl.StartsWith(LoginPath, StringComparison.OrdinalIgnoreCase);

            return isUsable ? returnUrl! : DashboardPath;
        }

        /// <summary>
        /// True when the caller already holds a valid session for this portal.
        /// Login actions use it to send them straight to the dashboard instead
        /// of showing a form they do not need.
        /// </summary>
        protected bool IsAlreadySignedIn =>
            User.Identity?.IsAuthenticated == true && PrincipalAccepted;

        protected LoginViewModel BuildLoginModel(string? returnUrl, bool denied)
        {
            if (denied)
            {
                ViewData["LoginWarning"] =
                    $"You are signed in, but not as a {RequiredRole}. Sign in with a {RequiredRole} account.";
            }

            return new LoginViewModel { ReturnUrl = returnUrl };
        }
    }
}
