using GigApp.Api.Dtos;
using GigApp.Api.Models;
using GigApp.Api.Services;
using GigApp.Api.Services.Bidding;
using GigApp.Api.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// Bidding on tasks. A partner no longer claims a task directly — they bid,
    /// the customer accepts or counters, and only an accepted bid assigns the
    /// task. Every rule sits in <see cref="IBidService"/> so the mobile apps get
    /// the same behaviour without reimplementing it.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [TrackForm("Bid")]
    public class BidsController : ControllerBase
    {
        private readonly IBidService _bids;

        public BidsController(IBidService bids) => _bids = bids;

        // POST: api/bids/task/5  -> place or update a bid
        [HttpPost("task/{taskId:int}")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<BidDto>> Place(
            int taskId, PlaceBidRequest request, CancellationToken ct) =>
            FromResult(await _bids.PlaceAsync(User.GetRequiredUserId(), taskId, request, ct));

        // GET: api/bids/mine
        [HttpGet("mine")]
        [Authorize(Policy = Policies.PartnerOnly)]
        public async Task<ActionResult<IEnumerable<BidDto>>> Mine(CancellationToken ct) =>
            Ok(await _bids.ForPartnerAsync(User.GetRequiredUserId(), ct));

        // POST: api/bids/5/withdraw
        [HttpPost("{bidId:int}/withdraw")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<ActionResult<BidDto>> Withdraw(int bidId, CancellationToken ct) =>
            FromResult(await _bids.WithdrawAsync(User.GetRequiredUserId(), bidId, ct));

        // POST: api/bids/5/accept-counter  -> partner takes the customer's counter
        [HttpPost("{bidId:int}/accept-counter")]
        [Authorize(Policy = Policies.PartnerOnly)]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<ActionResult<BidDto>> AcceptCounter(int bidId, CancellationToken ct) =>
            FromResult(await _bids.AcceptCounterAsync(User.GetRequiredUserId(), bidId, ct));

        // GET: api/bids/task/5  -> bids on the caller's own task
        [HttpGet("task/{taskId:int}")]
        [Authorize(Policy = Policies.CustomerOnly)]
        public async Task<ActionResult<IEnumerable<BidDto>>> ForTask(int taskId, CancellationToken ct) =>
            Ok(await _bids.ForTaskAsync(User.GetRequiredUserId(), taskId, ct));

        // POST: api/bids/5/counter
        [HttpPost("{bidId:int}/counter")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<ActionResult<BidDto>> Counter(
            int bidId, CounterBidRequest request, CancellationToken ct) =>
            FromResult(await _bids.CounterAsync(User.GetRequiredUserId(), bidId, request, ct));

        // POST: api/bids/5/accept  -> assigns the task
        [HttpPost("{bidId:int}/accept")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<ActionResult<BidDto>> Accept(int bidId, CancellationToken ct) =>
            FromResult(await _bids.AcceptAsync(User.GetRequiredUserId(), bidId, ct));

        // POST: api/bids/5/reject
        [HttpPost("{bidId:int}/reject")]
        [Authorize(Policy = Policies.CustomerOnly)]
        [TrackEntry(TrackingEntryType.Update)]
        public async Task<ActionResult<BidDto>> Reject(int bidId, CancellationToken ct) =>
            FromResult(await _bids.RejectAsync(User.GetRequiredUserId(), bidId, ct));

        private ActionResult<BidDto> FromResult(BidResult result) =>
            result.Succeeded
                ? Ok(result.Bid)
                : BadRequest(new ProblemDetails { Title = result.Error, Status = 400 });
    }
}
