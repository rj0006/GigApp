using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Banking;
using GigApp.Api.Services.Orders;
using GigApp.Api.Services.Profile;
using GigApp.Api.Services.Ratings;
using GigApp.Api.Services.Support;
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
        private readonly IBankAccountService _bankAccounts;
        private readonly IOrderHistoryService _orderHistory;
        private readonly IRatingService _ratings;
        private readonly ISupportService _support;

        public ProfileController(
            IProfileService profile,
            IBankAccountService bankAccounts,
            IOrderHistoryService orderHistory,
            IRatingService ratings,
            ISupportService support)
        {
            _profile = profile;
            _bankAccounts = bankAccounts;
            _orderHistory = orderHistory;
            _ratings = ratings;
            _support = support;
        }

        // GET: api/profile/bank
        [HttpGet("bank")]
        [TrackForm("BankAccount")]
        public async Task<ActionResult<BankAccountDto>> GetBankAccount(CancellationToken ct)
        {
            var account = await _bankAccounts.GetAsync(User.GetRequiredUserId(), ct);
            return account is null ? NoContent() : Ok(account);
        }

        // PUT: api/profile/bank
        [HttpPut("bank")]
        [TrackForm("BankAccount")]
        public async Task<ActionResult<BankAccountDto>> SaveBankAccount(
            SaveBankAccountRequest request, CancellationToken ct)
        {
            var result = await _bankAccounts.SaveAsync(User.GetRequiredUserId(), request, ct);

            return result.Succeeded
                ? Ok(result.Account)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
        }

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

        // GET: api/profile/orders?page=&pageSize=&search=&status=
        [HttpGet("orders")]
        public async Task<ActionResult<OrderHistoryPageDto>> GetOrders(
            [FromQuery] PageRequest paging, string? status, CancellationToken ct)
        {
            var userId = User.GetRequiredUserId();
            var role = User.IsInRole(UserRoles.Partner) ? UserRoles.Partner : UserRoles.Customer;

            var orders = await _orderHistory.ForUserAsync(userId, role, paging, status, ct);
            var ids = orders.Items.Select(o => o.Id).ToList();

            return Ok(new OrderHistoryPageDto
            {
                Orders = orders,
                IsPartner = role == UserRoles.Partner,
                Ratings = await _ratings.ForTasksAsync(ids, role, ct),
                Enquiries = await _support.LatestForTasksAsync(userId, ids, ct),
            });
        }

        // POST: api/profile/orders/5/help
        [HttpPost("orders/{id:int}/help")]
        [TrackForm("SupportEnquiry")]
        public async Task<ActionResult<SupportEnquiryDto>> RaiseEnquiry(
            int id, RaiseEnquiryRequest request, CancellationToken ct)
        {
            var role = User.IsInRole(UserRoles.Partner) ? UserRoles.Partner : UserRoles.Customer;
            var result = await _support.RaiseAsync(User.GetRequiredUserId(), role, id, request, ct);

            return result.Succeeded
                ? Ok(result.Enquiry)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
        }

        private ActionResult<UserDto> FromResult(ProfileResult result) =>
            result.Succeeded
                ? Ok(result.User)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
    }
}
