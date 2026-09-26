using GigApp.Api.Dtos;
using GigApp.Api.Services;
using GigApp.Api.Services.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.AdminOnly)]
    public class EnquiriesController : ControllerBase
    {
        private readonly ISupportService _support;

        public EnquiriesController(ISupportService support)
        {
            _support = support;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<SupportEnquiryDto>>> GetAllPaged(
            [FromQuery] PageRequest paging, string? status, CancellationToken ct) =>
            Ok(await _support.ListAsync(paging, status, ct));

        [HttpPost("{id:int}/review")]
        public async Task<ActionResult<SupportEnquiryDto>> Review(
            int id, ResolveEnquiryRequest request, CancellationToken ct)
        {
            var result = await _support.ReviewAsync(User.GetRequiredUserId(), id, request, ct);

            return result.Succeeded
                ? Ok(result.Enquiry)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
        }
    }
}
