using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Dtos
{
    /// <summary>Query-string paging input. Bind with [FromQuery].</summary>
    public class PageRequest
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 100;

        private int _page = 1;
        private int _pageSize = DefaultPageSize;

        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        /// <summary>Clamped so a caller cannot ask for the whole table.</summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
        }

        public string? Search { get; set; }

        public int Skip => (Page - 1) * PageSize;
    }

    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public string? Search { get; set; }

        public int TotalPages => PageSize < 1 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;

        public int FirstRow => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int LastRow => Math.Min(Page * PageSize, TotalCount);

        public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) => new()
        {
            Items = Items.Select(selector).ToList(),
            Page = Page,
            PageSize = PageSize,
            TotalCount = TotalCount,
            Search = Search,
        };
    }

    public static class PagingExtensions
    {
        /// <summary>
        /// Runs COUNT + one page of rows. Always paginate at the database —
        /// never load a full list and page it in memory.
        /// </summary>
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> query, PageRequest request, CancellationToken ct = default)
        {
            var totalCount = await query.CountAsync(ct);

            // If a deletion pushed the caller past the last page, show the last one.
            var page = request.Page;
            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
            if (page > totalPages && totalPages > 0) page = totalPages;

            var items = await query
                .Skip((page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            return new PagedResult<T>
            {
                Items = items,
                Page = page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                Search = request.Search,
            };
        }
    }
}
