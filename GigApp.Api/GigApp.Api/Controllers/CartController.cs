using GigApp.Api.Dtos;
using GigApp.Api.Services.Storefront;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [Route("api/cart")]
    [ApiController]
    [AllowAnonymous]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cart;

        public CartController(ICartService cart) => _cart = cart;

        [HttpGet]
        public async Task<ActionResult<CartViewDto>> Get(CancellationToken ct) =>
            Ok(await _cart.PriceAsync(ct));

        [HttpPost("items")]
        public async Task<ActionResult<CartViewDto>> Add(AddCartItemRequest request, CancellationToken ct)
        {
            await _cart.AddAsync(request.ServiceItemId, request.Quantity, ct);
            return Ok(await _cart.PriceAsync(ct));
        }

        [HttpPut("items/{serviceItemId:int}")]
        public async Task<ActionResult<CartViewDto>> SetQuantity(
            int serviceItemId, SetCartQuantityRequest request, CancellationToken ct)
        {
            await _cart.SetQuantityAsync(serviceItemId, request.Quantity, ct);
            return Ok(await _cart.PriceAsync(ct));
        }

        [HttpDelete("items/{serviceItemId:int}")]
        public async Task<ActionResult<CartViewDto>> Remove(int serviceItemId, CancellationToken ct)
        {
            await _cart.SetQuantityAsync(serviceItemId, 0, ct);
            return Ok(await _cart.PriceAsync(ct));
        }
    }
}
