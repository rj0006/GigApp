using GigApp.Api.Dtos;
using Microsoft.AspNetCore.Http;

namespace GigApp.Api.ViewModels
{
    /// <summary>
    /// Everything the _Pagination partial needs. Built from a PagedResult plus
    /// the current request, so paging links keep whatever filters are applied.
    /// </summary>
    public class PagerModel
    {
        public int CurrentPage { get; init; }
        public int TotalPages { get; init; }
        public int TotalCount { get; init; }
        public int FirstRow { get; init; }
        public int LastRow { get; init; }

        public string Path { get; init; } = "/";

        /// <summary>Existing query values, minus "page".</summary>
        public IReadOnlyList<KeyValuePair<string, string>> CarryOver { get; init; } =
            Array.Empty<KeyValuePair<string, string>>();

        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;

        public static PagerModel From<T>(PagedResult<T> page, HttpContext http)
        {
            var carry = http.Request.Query
                .Where(q => !string.Equals(q.Key, "page", StringComparison.OrdinalIgnoreCase))
                .SelectMany(q => q.Value
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Select(v => new KeyValuePair<string, string>(q.Key, v!)))
                .ToList();

            return new PagerModel
            {
                CurrentPage = page.Page,
                TotalPages = page.TotalPages,
                TotalCount = page.TotalCount,
                FirstRow = page.FirstRow,
                LastRow = page.LastRow,
                Path = http.Request.Path,
                CarryOver = carry,
            };
        }

        public string UrlFor(int page)
        {
            var clamped = Math.Clamp(page, 1, Math.Max(TotalPages, 1));

            var parts = CarryOver
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}")
                .Append($"page={clamped}");

            return $"{Path}?{string.Join("&", parts)}";
        }

        /// <summary>Page numbers to render; -1 means an ellipsis gap.</summary>
        public IReadOnlyList<int> VisiblePages
        {
            get
            {
                const int window = 2;   // pages either side of the current one
                var pages = new List<int>();
                var last = 0;

                for (var i = 1; i <= TotalPages; i++)
                {
                    var isEdge = i == 1 || i == TotalPages;
                    var isNear = Math.Abs(i - CurrentPage) <= window;
                    if (!isEdge && !isNear) continue;

                    if (last > 0 && i - last > 1) pages.Add(-1);
                    pages.Add(i);
                    last = i;
                }

                return pages;
            }
        }
    }
}
