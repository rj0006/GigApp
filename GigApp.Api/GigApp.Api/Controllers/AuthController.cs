using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Services;
using GigApp.Api.Services.Kyc;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// Registration and login. There is deliberately no admin registration
    /// endpoint — admin accounts are seeded or created from the admin panel.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly AppDbContext _context;

        public AuthController(IAuthService auth, AppDbContext context)
        {
            _auth = auth;
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
            [FromForm] RegisterPartnerRequest request, CancellationToken ct)
        {
            var result = await _auth.RegisterPartnerAsync(request, ct);

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
