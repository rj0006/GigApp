using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Profile;
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
        protected readonly IProfileService ProfileService;
        protected readonly IAddressService AddressService;

        protected PortalControllerBase(
            IAuthService authService,
            IProfileService profileService,
            IAddressService addressService)
        {
            AuthService = authService;
            ProfileService = profileService;
            AddressService = addressService;
        }

        protected string ProfilePath => $"/{PortalSlug}/profile";
        protected string AddressesPath => $"/{PortalSlug}/addresses";

        /// <summary>
        /// Every portal view needs its own home link, so the navbar brand points
        /// at the dashboard rather than the public landing page.
        /// </summary>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            ViewData["PortalHome"] = User.Identity?.IsAuthenticated == true && User.IsInRole(RequiredRole)
                ? DashboardPath
                : "/";

            base.OnActionExecuting(context);
        }

        /// <summary>Route prefix and role this portal serves, e.g. "customer".</summary>
        protected abstract string PortalSlug { get; }
        protected abstract string RequiredRole { get; }

        protected string LoginPath => $"/{PortalSlug}/login";
        protected string DashboardPath => $"/{PortalSlug}";

        // ------------------------------------------------------------ profile
        // Declared once here so /admin/profile, /provider/profile and
        // /customer/profile all work from a single implementation — attribute
        // routes on a base action are picked up under each derived prefix.
        // Every one of these calls IProfileService, the same service the API
        // uses, so the rules cannot drift between web and mobile.

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> Profile(CancellationToken ct)
        {
            ViewData["Title"] = "My profile";

            var userId = User.GetRequiredUserId();
            var user = await ProfileService.GetAsync(userId, ct);

            if (user is null) return Redirect(LoginPath);

            return View("Profile", new ProfilePageViewModel
            {
                User = user,
                Form = new UpdateProfileRequest
                {
                    Name = user.Name,
                    Phone = user.Phone,
                    Email = user.Email,
                },
                History = await ProfileService.GetHistoryAsync(userId, 20, ct),
            });
        }

        [HttpPost("profile")]
        [Authorize]
        [ValidateAntiForgeryToken]
        [TrackForm("Profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ProfileError(FirstError());

            var result = await ProfileService.UpdateAsync(User.GetRequiredUserId(), form, ct);
            if (!result.Succeeded) return ProfileError(result.Error);

            TrackDoc(result.User!.Id, result.User);
            TempData["Success"] = "Profile updated.";
            return Redirect(ProfilePath);
        }

        [HttpPost("profile/photo")]
        [Authorize]
        [ValidateAntiForgeryToken]
        [TrackForm("Profile")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> UpdateProfilePhoto(IFormFile? photo, CancellationToken ct)
        {
            var result = await ProfileService.UpdatePhotoAsync(User.GetRequiredUserId(), photo, ct);
            if (!result.Succeeded) return ProfileError(result.Error);

            TrackDoc(result.User!.Id, result.User);
            TempData["Success"] = "Profile photo updated.";
            return Redirect(ProfilePath);
        }

        [HttpPost("profile/password")]
        [Authorize]
        [ValidateAntiForgeryToken]
        [TrackForm("Profile")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ProfileError(FirstError());

            var result = await ProfileService.ChangePasswordAsync(User.GetRequiredUserId(), form, ct);
            if (!result.Succeeded) return ProfileError(result.Error);

            TrackDoc(result.User!.Id, result.User);
            TempData["Success"] = "Password changed. Use the new one next time you sign in.";
            return Redirect(ProfilePath);
        }

        // ---------------------------------------------------------- addresses
        // Declared here for the same reason as profile: one implementation,
        // reachable as /customer/addresses and /provider/addresses.

        [HttpGet("addresses")]
        [Authorize]
        public async Task<IActionResult> Addresses(CancellationToken ct)
        {
            ViewData["Title"] = "My addresses";

            return View("Addresses", new AddressBookViewModel
            {
                Addresses = await AddressService.ListAsync(User.GetRequiredUserId(), ct),
            });
        }

        [HttpPost("addresses")]
        [Authorize]
        [ValidateAntiForgeryToken]
        [TrackForm("Address")]
        public async Task<IActionResult> CreateAddress(SaveAddressRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid) return AddressError(FirstError());

            var result = await AddressService.CreateAsync(User.GetRequiredUserId(), form, ct);
            if (!result.Succeeded) return AddressError(result.Error);

            TrackDoc(result.Address!.Id, result.Address);
            TempData["Success"] = $"'{result.Address.Label}' address saved.";
            return Redirect(AddressesPath);
        }

        [HttpPost("addresses/{id:int}/edit")]
        [Authorize]
        [ValidateAntiForgeryToken]
        [TrackForm("Address")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> UpdateAddress(int id, SaveAddressRequest form, CancellationToken ct)
        {
            if (!ModelState.IsValid) return AddressError(FirstError());

            var result = await AddressService.UpdateAsync(User.GetRequiredUserId(), id, form, ct);
            if (!result.Succeeded) return AddressError(result.Error);

            TrackDoc(id, result.Address);
            TempData["Success"] = "Address updated.";
            return Redirect(AddressesPath);
        }

        [HttpPost("addresses/{id:int}/default")]
        [Authorize]
        [ValidateAntiForgeryToken]
        [TrackForm("Address")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<IActionResult> SetDefaultAddress(int id, CancellationToken ct)
        {
            var result = await AddressService.SetDefaultAsync(User.GetRequiredUserId(), id, ct);
            if (!result.Succeeded) return AddressError(result.Error);

            TrackDoc(id, result.Address);
            TempData["Success"] = $"'{result.Address!.Label}' is now your default address.";
            return Redirect(AddressesPath);
        }

        [HttpPost("addresses/{id:int}/delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        [TrackForm("Address")]
        [TrackEntry(TrackingEntryType.Delete)]
        public async Task<IActionResult> DeleteAddress(int id, CancellationToken ct)
        {
            var result = await AddressService.DeleteAsync(User.GetRequiredUserId(), id, ct);
            if (!result.Succeeded) return AddressError(result.Error);

            TrackDoc(id, result.Address);
            TempData["Success"] = "Address removed.";
            return Redirect(AddressesPath);
        }

        private IActionResult AddressError(string? message)
        {
            TempData["Error"] = message ?? "Could not save that address.";
            return Redirect(AddressesPath);
        }

        private IActionResult ProfileError(string? message)
        {
            TempData["Error"] = message ?? "Could not save your changes.";
            return Redirect(ProfilePath);
        }

        private string? FirstError() => ModelState
            .SelectMany(e => e.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

        /// <summary>
        /// Runs a login/registration attempt and, on success, issues the cookie.
        /// Returns null when it worked; otherwise the view to re-render.
        /// </summary>
        protected async Task<IActionResult?> SignInAsync(
            Func<Task<AuthResult>> attempt, string viewName, object model)
        {
            var result = await attempt();

            if (!result.Succeeded || result.Response is null)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Something went wrong.");
                return View(viewName, model);
            }

            // Stop a customer from signing in through the admin door.
            if (!string.Equals(result.Response.User.Role, RequiredRole, StringComparison.Ordinal))
            {
                ModelState.AddModelError(string.Empty,
                    $"This is the {PortalSlug} portal. Your account is registered as a {result.Response.User.Role}.");
                return View(viewName, model);
            }

            IssueAuthCookie(result.Response);
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
        }

        protected void ClearAuthCookie() => Response.Cookies.Delete(AuthCookie.Name);

        /// <summary>
        /// Hands the saved record to the audit filter. Portal actions redirect,
        /// so unlike the API there is no returned DTO for the filter to read —
        /// call this after a successful save.
        /// </summary>
        protected void TrackDoc(object? docNo, object? result = null)
        {
            HttpContext.Items[TrackingKeys.DocNo] = docNo;
            HttpContext.Items[TrackingKeys.Result] = result;
        }

        /// <summary>
        /// Only follow a return URL that stays on this site, and never one that
        /// points back at the login page — that would loop forever once the
        /// caller is already signed in.
        /// </summary>
        protected IActionResult RedirectToLocalOr(string? returnUrl)
        {
            var isUsable = !string.IsNullOrEmpty(returnUrl)
                && Url.IsLocalUrl(returnUrl)
                && !returnUrl.StartsWith(LoginPath, StringComparison.OrdinalIgnoreCase);

            return Redirect(isUsable ? returnUrl! : DashboardPath);
        }

        /// <summary>
        /// True when the caller already holds a valid session for this portal.
        /// Login actions use it to send them straight to the dashboard instead
        /// of showing a form they do not need.
        /// </summary>
        protected bool IsAlreadySignedIn =>
            User.Identity?.IsAuthenticated == true && User.IsInRole(RequiredRole);

        protected LoginViewModel BuildLoginModel(string? returnUrl, bool denied)
        {
            if (denied)
            {
                ModelState.AddModelError(string.Empty,
                    $"You are signed in, but not as a {RequiredRole}. Sign in with a {RequiredRole} account.");
            }

            return new LoginViewModel { ReturnUrl = returnUrl };
        }
    }
}
