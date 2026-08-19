using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// Administrative user directory. Account creation lives in
    /// <see cref="AuthController"/> — there is no create endpoint here, because
    /// binding a User entity straight from the request body would let a caller
    /// set their own Role and PasswordHash.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.AdminOnly)]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/users?role=partner
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(
            [FromQuery] string? role, CancellationToken ct)
        {
            if (role is not null && !UserRoles.IsValid(role))
                return BadRequest($"Unknown role '{role}'.");

            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.PartnerProfile)
                .AsQueryable();

            if (role is not null)
                query = query.Where(u => u.Role == role);

            var users = await query
                .OrderBy(u => u.Id)
                .ToListAsync(ct);

            return Ok(users.Select(UserDto.From));
        }

        // GET: api/users/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserDto>> GetUserById(int id, CancellationToken ct)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.PartnerProfile)
                .FirstOrDefaultAsync(u => u.Id == id, ct);

            if (user is null) return NotFound();

            return Ok(UserDto.From(user));
        }
    }
}
