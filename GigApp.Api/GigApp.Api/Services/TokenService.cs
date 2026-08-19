using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GigApp.Api.Configuration;
using GigApp.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GigApp.Api.Services
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
    }

    public class TokenService : ITokenService
    {
        private readonly JwtSettings _settings;

        public TokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

        public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
        {
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

            // Short claim names throughout — inbound claim mapping is disabled in
            // Program.cs, so what is written here is exactly what is read back.
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimNames.Name, user.Name),
                new(ClaimNames.Role, user.Role),
                new(ClaimNames.Phone, user.Phone),
            };

            // Phone-first accounts may have no email at all.
            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAtUtc,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
        }
    }

    public static class ClaimNames
    {
        public const string Name = "name";
        public const string Role = "role";
        public const string Phone = "phone";
    }
}
