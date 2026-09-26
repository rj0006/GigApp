using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.UserAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.AdminOnly)]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUserAdminService _userAdmin;

        public UsersController(AppDbContext context, IUserAdminService userAdmin)
        {
            _context = context;
            _userAdmin = userAdmin;
        }

        // GET: api/users/all?role=customer&page=1&pageSize=10&search=&includeSuperAdmins=true
        [HttpGet("all")]
        public async Task<ActionResult<PagedResult<UserDto>>> GetAllPaged(
            [FromQuery] PageRequest paging, string role, bool includeSuperAdmins, CancellationToken ct)
        {
            if (!UserRoles.IsValid(role))
                return BadRequest($"Unknown role '{role}'.");

            var query = _context.Users.AsNoTracking()
                .Include(u => u.PartnerProfile)!.ThenInclude(p => p!.SkillCategory)
                .Where(u => u.Role == role || (includeSuperAdmins && u.Role == UserRoles.SuperAdmin));

            if (!string.IsNullOrWhiteSpace(paging.Search))
            {
                var term = paging.Search.Trim().ToLower();
                query = query.Where(u =>
                    u.Name.ToLower().Contains(term) ||
                    u.Phone.Contains(term) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)));
            }

            var page = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToPagedResultAsync(paging, ct);

            return Ok(page.Map(UserDto.From));
        }

        // POST: api/users/5/reset-password
        [HttpPost("{id:int}/reset-password")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<ActionResult<UserAdminResult>> ResetPassword(
            int id, AdminResetPasswordRequest request, CancellationToken ct)
        {
            var result = await _userAdmin.ResetPasswordAsync(User.GetRequiredUserId(), id, request, ct);

            if (!result.Succeeded)
                return UnprocessableEntity(new ProblemDetails { Title = result.Error, Status = 422 });

            return Ok(result);
        }

        // POST: api/users/5/active
        [HttpPost("{id:int}/active")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<ActionResult<UserAdminResult>> SetActive(
            int id, SetUserActiveRequest request, CancellationToken ct)
        {
            var result = await _userAdmin.SetActiveAsync(User.GetRequiredUserId(), id, request, ct);

            if (!result.Succeeded)
                return UnprocessableEntity(new ProblemDetails { Title = result.Error, Status = 422 });

            return Ok(result);
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
