using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// The signed-in user's own account. Every role uses the same endpoints —
    /// the user id always comes from the token, never from the payload, so one
    /// user can never edit another.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [TrackForm("Profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profile;

        public ProfileController(IProfileService profile) => _profile = profile;

        // GET: api/profile
        [HttpGet]
        public async Task<ActionResult<UserDto>> Get(CancellationToken ct)
        {
            var user = await _profile.GetAsync(User.GetRequiredUserId(), ct);
            return user is null ? Unauthorized() : Ok(user);
        }

        // PUT: api/profile
        [HttpPut]
        public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest request, CancellationToken ct)
        {
            var result = await _profile.UpdateAsync(User.GetRequiredUserId(), request, ct);
            return FromResult(result);
        }

        // POST: api/profile/photo
        [HttpPost("photo")]
        [TrackEntry(TrackingEntryType.Update)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<UserDto>> UpdatePhoto(
            [FromForm] UpdateProfilePhotoRequest request, CancellationToken ct)
        {
            var result = await _profile.UpdatePhotoAsync(User.GetRequiredUserId(), request.Photo, ct);
            return FromResult(result);
        }

        // POST: api/profile/password
        [HttpPost("password")]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<ActionResult<UserDto>> ChangePassword(
            ChangePasswordRequest request, CancellationToken ct)
        {
            var result = await _profile.ChangePasswordAsync(User.GetRequiredUserId(), request, ct);
            return FromResult(result);
        }

        // GET: api/profile/history
        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<ProfileChangeDto>>> History(
            [FromQuery] int take, CancellationToken ct)
        {
            var history = await _profile.GetHistoryAsync(
                User.GetRequiredUserId(), take < 1 ? 20 : Math.Min(take, 100), ct);

            return Ok(history);
        }

        private ActionResult<UserDto> FromResult(ProfileResult result) =>
            result.Succeeded
                ? Ok(result.User)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
    }
}
