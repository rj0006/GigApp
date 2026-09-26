using GigApp.Api.Dtos;
using GigApp.Api.Services.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.SuperAdminOnly)]
    public class MenuItemsController : ControllerBase
    {
        private readonly IMenuService _menus;

        public MenuItemsController(IMenuService menus) => _menus = menus;

        // GET: api/menuitems
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> GetAll(CancellationToken ct) =>
            Ok(await _menus.GetAllAsync(ct));

        // GET: api/menuitems/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<MenuItemDto>> GetById(int id, CancellationToken ct)
        {
            var item = await _menus.GetAsync(id, ct);
            return item is null ? NotFound() : Ok(item);
        }

        // GET: api/menuitems/parent-options?excludingId=5
        [HttpGet("parent-options")]
        public async Task<ActionResult<IReadOnlyList<MenuOptionDto>>> GetParentOptions(
            int? excludingId, CancellationToken ct) =>
            Ok(await _menus.GetParentOptionsAsync(excludingId, ct));

        // POST: api/menuitems  -> Id absent creates, Id present updates that row
        [HttpPost]
        public async Task<ActionResult<MenuItemDto>> Save(SaveMenuItemRequest request, CancellationToken ct)
        {
            var result = await _menus.SaveAsync(request.Id, request, ct);

            if (!result.Succeeded)
                return UnprocessableEntity(new ProblemDetails { Title = result.Error, Status = 422 });

            return Ok(result.Item);
        }

        // DELETE: api/menuitems/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var result = await _menus.DeleteAsync(id, ct);

            if (!result.Succeeded)
                return UnprocessableEntity(new ProblemDetails { Title = result.Error, Status = 422 });

            return Ok(result.Item);
        }
    }
}
