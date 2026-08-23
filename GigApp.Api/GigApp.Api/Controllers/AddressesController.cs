using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Addresses;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// The caller's own saved addresses. The user id always comes from the
    /// token, so one user can never read or edit another's addresses.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [TrackForm("Address")]
    public class AddressesController : ControllerBase
    {
        private readonly IAddressService _addresses;

        public AddressesController(IAddressService addresses) => _addresses = addresses;

        // GET: api/addresses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AddressDto>>> List(CancellationToken ct) =>
            Ok(await _addresses.ListAsync(User.GetRequiredUserId(), ct));

        // GET: api/addresses/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<AddressDto>> Get(int id, CancellationToken ct)
        {
            var address = await _addresses.GetAsync(User.GetRequiredUserId(), id, ct);
            return address is null ? NotFound() : Ok(address);
        }

        // POST: api/addresses
        [HttpPost]
        public async Task<ActionResult<AddressDto>> Create(SaveAddressRequest request, CancellationToken ct) =>
            FromResult(await _addresses.CreateAsync(User.GetRequiredUserId(), request, ct), created: true);

        // PUT: api/addresses/5
        [HttpPut("{id:int}")]
        public async Task<ActionResult<AddressDto>> Update(
            int id, SaveAddressRequest request, CancellationToken ct) =>
            FromResult(await _addresses.UpdateAsync(User.GetRequiredUserId(), id, request, ct));

        // PUT: api/addresses/5/default
        [HttpPut("{id:int}/default")]
        public async Task<ActionResult<AddressDto>> SetDefault(int id, CancellationToken ct) =>
            FromResult(await _addresses.SetDefaultAsync(User.GetRequiredUserId(), id, ct));

        // DELETE: api/addresses/5
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<AddressDto>> Delete(int id, CancellationToken ct) =>
            FromResult(await _addresses.DeleteAsync(User.GetRequiredUserId(), id, ct));

        private ActionResult<AddressDto> FromResult(AddressResult result, bool created = false)
        {
            if (!result.Succeeded)
                return BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });

            return created
                ? StatusCode(StatusCodes.Status201Created, result.Address)
                : Ok(result.Address);
        }
    }
}
