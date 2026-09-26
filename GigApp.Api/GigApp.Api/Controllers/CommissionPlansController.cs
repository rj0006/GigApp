using GigApp.Api.Dtos;
using GigApp.Api.Services.Earnings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.AdminOnly)]
    public class CommissionPlansController : ControllerBase
    {
        private readonly IPlanService _plans;

        public CommissionPlansController(IPlanService plans) => _plans = plans;

        // GET: api/commissionplans?showInactive=true
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<CommissionPlanDto>>> GetAll(
            bool showInactive, CancellationToken ct)
        {
            return Ok(await _plans.GetPlansAsync(!showInactive, ct));
        }

        // GET: api/commissionplans/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<ActionResult<CommissionPlanDto>> GetById(int id, CancellationToken ct)
        {
            var plan = await _plans.GetPlanAsync(id, ct);
            return plan is null ? NotFound() : Ok(plan);
        }

        // POST: api/commissionplans  -> Id absent creates, Id present updates that row
        [HttpPost]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<ActionResult<CommissionPlanDto>> Save(SaveCommissionPlanRequest request, CancellationToken ct)
        {
            var result = await _plans.SavePlanAsync(request.Id, request, ct);

            if (!result.Succeeded)
                return UnprocessableEntity(new ProblemDetails { Title = result.Error, Status = 422 });

            return Ok(result.Plan);
        }
    }
}
