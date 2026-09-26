using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Otp;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// Registration and login. There is deliberately no admin registration
    /// endpoint — admin accounts are seeded or created from the admin panel.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting(RateLimiterPolicies.Auth)]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IRefreshTokenService _refreshTokens;
        private readonly IOtpService _otp;
        private readonly AppDbContext _context;

        public AuthController(
            IAuthService auth, IRefreshTokenService refreshTokens, IOtpService otp, AppDbContext context)
        {
            _auth = auth;
            _refreshTokens = refreshTokens;
            _otp = otp;
            _context = context;
        }

        // POST: api/auth/register/customer
        [HttpPost("register/customer")]
        [TrackForm("Customer")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponse>> RegisterCustomer(
            RegisterCustomerRequest request, CancellationToken ct)
        {
            var result = await _auth.RegisterCustomerAsync(request, ct);
            return FromResult(result, created: true);
        }

        // POST: api/auth/register/partner
        // Multipart, not JSON — a partner uploads a selfie and both sides of
        // their Aadhaar card as part of signing up.
        [HttpPost("register/partner")]
        [TrackForm(KycHistoryService.FormType)]
        [AllowAnonymous]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponse>> RegisterPartner(
            [FromForm] RegisterPartnerRequest request, [FromForm] bool phoneVerifiedViaOtp, CancellationToken ct)
        {
            var result = await _auth.RegisterPartnerAsync(request, ct);

            if (phoneVerifiedViaOtp && result.Response is not null)
            {
                await _context.Users
                    .Where(u => u.Id == result.Response.User.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.IsPhoneVerified, true)
                        .SetProperty(u => u.PhoneVerifiedAt, DateTime.UtcNow), ct);
            }

            var profile = result.Response?.User.PartnerProfile;
            if (profile is not null) HttpContext.TrackDoc(profile.Id, profile);

            return FromResult(result, created: true);
        }

        // POST: api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        [SkipTracking]   // signing in changes no data
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        {
            var result = await _auth.LoginAsync(request, ct);

            if (!result.Succeeded)
                return Unauthorized(new ProblemDetails { Title = result.Error, Status = 401 });

            return Ok(result.Response);
        }

        // POST: api/auth/otp/request
        // Mobile-facing counterpart of the portals' own otp/request action —
        // those set a cookie and cannot be called from a client with no cookie jar.
        [HttpPost("otp/request")]
        [AllowAnonymous]
        [SkipTracking]
        [EnableRateLimiting(RateLimiterPolicies.OtpRequest)]
        public async Task<IActionResult> RequestOtp(OtpRequestApiRequest request, CancellationToken ct)
        {
            if (!IsSignInRole(request.Role))
                return BadRequest(new ProblemDetails { Title = "Choose a valid role.", Status = 400 });

            var result = await _otp.RequestAsync(request.Phone, request.Role, ct);

            if (!result.Succeeded)
                return BadRequest(new ProblemDetails { Title = result.Error ?? "Could not send the code.", Status = 400 });

            return Ok(new { devCode = result.DevCode });
        }

        // POST: api/auth/otp/verify
        [HttpPost("otp/verify")]
        [AllowAnonymous]
        [SkipTracking]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OtpVerifyApiResponse>> VerifyOtp(OtpVerifyApiRequest request, CancellationToken ct)
        {
            if (!IsSignInRole(request.Role))
                return BadRequest(new ProblemDetails { Title = "Choose a valid role.", Status = 400 });

            var result = await _otp.VerifyAsync(request.Phone, request.Role, request.Code, request.Name, ct);

            if (result.RequiresPartnerRegistration)
                return Ok(new OtpVerifyApiResponse { RequiresPartnerRegistration = true });

            if (!result.Succeeded || result.Auth?.Response is null)
                return BadRequest(new ProblemDetails { Title = result.Error ?? "Could not verify that code.", Status = 400 });

            return Ok(new OtpVerifyApiResponse { Auth = result.Auth.Response });
        }

        private static bool IsSignInRole(string role) => role is UserRoles.Customer or UserRoles.Partner;

        // POST: api/auth/refresh
        // The client's own copy of its refresh token is spent and replaced on
        // every call — a token can only ever be redeemed once.
        [HttpPost("refresh")]
        [AllowAnonymous]
        [SkipTracking]   // refreshing changes no data the caller cares about
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
        {
            var deviceLabel = DeviceLabel.FromUserAgent(Request.Headers.UserAgent.ToString());
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await _refreshTokens.RedeemAsync(request.RefreshToken, deviceLabel, ipAddress, ct);

            if (!result.Succeeded)
                return Unauthorized(new ProblemDetails { Title = result.Error, Status = 401 });

            return Ok(result.Response);
        }

        // GET: api/auth/me
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.PartnerProfile)
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            // The token is valid but the account is gone — treat as unauthenticated.
            if (user is null) return Unauthorized();

            return Ok(UserDto.From(user));
        }

        private ActionResult<AuthResponse> FromResult(AuthResult result, bool created)
        {
            if (!result.Succeeded)
                return BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });

            return created
                ? StatusCode(StatusCodes.Status201Created, result.Response)
                : Ok(result.Response);
        }
    }
}
