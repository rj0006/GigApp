using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Services;
using GigApp.Api.Services.Banking;
using GigApp.Api.Services.Earnings;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.AdminOnly)]
    public class EarningsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEarningsService _earnings;
        private readonly IPlanService _plans;
        private readonly IBankAccountService _bankAccounts;

        public EarningsController(
            AppDbContext context,
            IEarningsService earnings,
            IPlanService plans,
            IBankAccountService bankAccounts)
        {
            _context = context;
            _earnings = earnings;
            _plans = plans;
            _bankAccounts = bankAccounts;
        }

        [HttpGet("balances")]
        public async Task<ActionResult<IReadOnlyList<PartnerBalanceDto>>> GetBalances(CancellationToken ct) =>
            Ok(await _earnings.GetBalancesAsync(ct));

        [HttpGet("{partnerId:int}")]
        public async Task<ActionResult<PartnerLedgerPageDto>> GetPartnerPage(int partnerId, CancellationToken ct)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.SkillCategory)
                .FirstOrDefaultAsync(p => p.Id == partnerId, ct);

            if (partner is null) return NotFound();

            return Ok(new PartnerLedgerPageDto
            {
                Partner = PartnerDto.From(partner),
                Summary = await _earnings.GetSummaryAsync(partnerId, ct),
                BankAccount = await _bankAccounts.GetAsync(partner.UserId, ct),
                CurrentPlan = await _plans.GetPartnerPlanAsync(partnerId, ct),
                AvailablePlans = await _plans.GetPlansAsync(true, ct),
                PlanHistory = await _plans.GetPartnerPlanHistoryAsync(partnerId, ct),
            });
        }

        [HttpGet("{partnerId:int}/entries")]
        public async Task<ActionResult<PagedResult<LedgerEntryDto>>> GetEntries(
            int partnerId, [FromQuery] PageRequest paging, string? entryType, CancellationToken ct) =>
            Ok(await _earnings.GetEntriesAsync(partnerId, paging, entryType, ct));

        [HttpPost("{partnerId:int}/payout")]
        [TrackForm("Payout")]
        public async Task<ActionResult<LedgerEntryDto>> RecordPayout(
            int partnerId, RecordPayoutRequest request, CancellationToken ct)
        {
            var result = await _earnings.PostPayoutAsync(
                partnerId, request, User.GetRequiredUserId(), User.Identity?.Name, ct);

            return result.Succeeded
                ? Ok(result.Entry)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
        }

        [HttpPost("{partnerId:int}/adjustment")]
        [Authorize(Policy = Policies.SuperAdminOnly)]
        [TrackForm("Payout")]
        public async Task<ActionResult<LedgerEntryDto>> RecordAdjustment(
            int partnerId, RecordAdjustmentRequest request, CancellationToken ct)
        {
            var result = await _earnings.PostAdjustmentAsync(
                partnerId, request, User.GetRequiredUserId(), User.Identity?.Name, ct);

            return result.Succeeded
                ? Ok(result.Entry)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
        }

        [HttpPost("{partnerId:int}/plan")]
        [TrackForm("CommissionPlan")]
        public async Task<ActionResult<PartnerPlanDto>> AssignPlan(
            int partnerId, AssignPlanRequest request, CancellationToken ct)
        {
            var result = await _plans.AssignPlanAsync(
                partnerId, request, User.GetRequiredUserId(), User.Identity?.Name, ct);

            if (!result.Succeeded)
                return BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });

            if (result.ChargeFee)
            {
                await _earnings.PostSubscriptionFeeAsync(
                    partnerId, result.PlanId, User.GetRequiredUserId(), User.Identity?.Name, ct);
            }

            return Ok(PartnerPlanDto.From(result.Subscription!));
        }
    }
}
