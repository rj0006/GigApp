using GigApp.Api.Dtos;
using GigApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    // The caller's own signed-in devices — each one a live, non-revoked
    // refresh token. There is no cross-user listing; a session belongs to
    // exactly the account that redeemed it.
    [Route("api/devices")]
    [ApiController]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly IRefreshTokenService _refreshTokens;

        public DevicesController(IRefreshTokenService refreshTokens) => _refreshTokens = refreshTokens;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DeviceSessionDto>>> List(
            [FromHeader(Name = "X-Refresh-Token")] string? mobileRefreshToken, CancellationToken ct)
        {
            var currentToken = CurrentRefreshToken(mobileRefreshToken);
            var list = await _refreshTokens.ListActiveAsync(User.GetRequiredUserId(), currentToken, ct);
            return Ok(list);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Revoke(int id, CancellationToken ct)
        {
            var revoked = await _refreshTokens.RevokeAsync(User.GetRequiredUserId(), id, ct);
            return revoked ? NoContent() : NotFound();
        }

        [HttpPost("revoke-others")]
        public async Task<IActionResult> RevokeOthers(
            [FromHeader(Name = "X-Refresh-Token")] string? mobileRefreshToken, CancellationToken ct)
        {
            var currentToken = CurrentRefreshToken(mobileRefreshToken);
            await _refreshTokens.RevokeAllExceptAsync(User.GetRequiredUserId(), currentToken, ct);
            return NoContent();
        }

        // The web portals carry the refresh token in the gigapp_refresh cookie; mobile has no
        // cookie jar, so it sends the same raw token it already holds via this header instead —
        // this is the only way either client can be told apart from "some other device" here.
        private string? CurrentRefreshToken(string? mobileRefreshToken)
        {
            Request.Cookies.TryGetValue(AuthCookie.RefreshName, out var cookieToken);
            return cookieToken ?? mobileRefreshToken;
        }
    }
}
