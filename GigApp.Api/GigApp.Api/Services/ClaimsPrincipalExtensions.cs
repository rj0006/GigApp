using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GigApp.Api.Models;

namespace GigApp.Api.Services
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>The authenticated user's id, or null when unauthenticated.</summary>
        public static int? GetUserId(this ClaimsPrincipal principal)
        {
            var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var id) ? id : null;
        }

        /// <summary>The user's id, throwing if absent. Use inside [Authorize] endpoints.</summary>
        public static int GetRequiredUserId(this ClaimsPrincipal principal) =>
            principal.GetUserId()
            ?? throw new InvalidOperationException("No user id claim on an authenticated principal.");

        public static string? GetRole(this ClaimsPrincipal principal) =>
            principal.FindFirstValue(ClaimNames.Role);

        public static bool IsAdmin(this ClaimsPrincipal principal) =>
            principal.IsInRole(UserRoles.Admin);
    }
}
