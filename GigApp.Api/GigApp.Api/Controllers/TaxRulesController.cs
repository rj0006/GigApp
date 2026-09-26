using GigApp.Api.Dtos;
using GigApp.Api.Services.Earnings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.AdminOnly)]
    public class TaxRulesController : ControllerBase
    {
        private readonly ITaxService _taxes;

        public TaxRulesController(ITaxService taxes) => _taxes = taxes;

        // GET: api/taxrules?country=IN&showInactive=true
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TaxRuleDto>>> GetAll(
            string? country, bool showInactive, CancellationToken ct)
        {
            return Ok(await _taxes.GetRulesAsync(country, !showInactive, ct));
        }

        // GET: api/taxrules/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<ActionResult<TaxRuleDto>> GetById(int id, CancellationToken ct)
        {
            var rule = await _taxes.GetRuleAsync(id, ct);
            return rule is null ? NotFound() : Ok(rule);
        }

        // POST: api/taxrules  -> Id absent creates, Id present updates that row
        [HttpPost]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        public async Task<ActionResult<TaxRuleDto>> Save(SaveTaxRuleRequest request, CancellationToken ct)
        {
            var result = await _taxes.SaveRuleAsync(request.Id, request, ct);

            if (!result.Succeeded)
                return UnprocessableEntity(new ProblemDetails { Title = result.Error, Status = 422 });

            return Ok(result.Rule);
        }
    }
}
