using GigApp.Api.Dtos;
using GigApp.Api.Services.Masters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    /// <summary>
    /// One endpoint for every master list. global.js AutoComplete() points here,
    /// so a new master needs no new controller and no new JS.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MastersController : ControllerBase
    {
        private const int DefaultLimit = 20;
        private const int MaxLimit = 50;

        private readonly IMasterRegistry _registry;

        public MastersController(IMasterRegistry registry) => _registry = registry;

        // GET: api/masters/skill-category?term=plum&limit=20
        [HttpGet("{key}")]
        public async Task<ActionResult<ApiResponse<IReadOnlyList<MasterItemDto>>>> Search(
            string key,
            [FromQuery] string? term,
            [FromQuery] int limit,
            CancellationToken ct)
        {
            var source = _registry.Find(key);

            if (source is null)
                return NotFound(ApiResponse<IReadOnlyList<MasterItemDto>>.Fail(
                    $"Unknown master '{key}'. Known: {string.Join(", ", _registry.Keys)}"));

            var take = limit < 1 ? DefaultLimit : Math.Min(limit, MaxLimit);
            var items = await source.SearchAsync(term, take, ct);

            return Ok(ApiResponse<IReadOnlyList<MasterItemDto>>.Ok(items));
        }

        // GET: api/masters  -> what masters exist
        [HttpGet]
        public ActionResult<ApiResponse<IReadOnlyList<string>>> List() =>
            Ok(ApiResponse<IReadOnlyList<string>>.Ok(_registry.Keys));
    }
}
