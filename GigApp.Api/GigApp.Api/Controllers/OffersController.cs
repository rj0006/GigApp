using GigApp.Api.Dtos;
using GigApp.Api.Services;
using GigApp.Api.Services.Booking;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.PartnerOnly)]
    [TrackForm("JobOffer")]
    public class OffersController : ControllerBase
    {
        private readonly IOfferService _offers;

        public OffersController(IOfferService offers)
        {
            _offers = offers;
        }

        [HttpPost("{id:int}/respond")]
        public async Task<IActionResult> Respond(int id, RespondToOfferRequest request, CancellationToken ct)
        {
            var result = await _offers.RespondAsync(User.GetRequiredUserId(), id, request.Accepted, ct);

            return result.Succeeded
                ? Ok()
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
        }
    }
}
