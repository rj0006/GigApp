using GigApp.Api.Dtos;

namespace GigApp.Api.Services.Masters
{
    /// <summary>
    /// One master list (skill categories, cities, payment modes …). Every master
    /// implements this, so a single endpoint can serve all of them and the front
    /// end never needs a new URL per master.
    ///
    /// To add a master: implement this, then register it in Program.cs with
    /// AddScoped&lt;IMasterSource, YourMaster&gt;(). Nothing else changes.
    /// </summary>
    public interface IMasterSource
    {
        /// <summary>URL segment, kebab-case. e.g. "skill-category".</summary>
        string Key { get; }

        /// <summary>Active rows matching <paramref name="term"/>; empty term = first N.</summary>
        Task<IReadOnlyList<MasterItemDto>> SearchAsync(string? term, int limit, CancellationToken ct);
    }

    /// <summary>Resolves a master by its URL key.</summary>
    public interface IMasterRegistry
    {
        IMasterSource? Find(string key);
        IReadOnlyList<string> Keys { get; }
    }

    public class MasterRegistry : IMasterRegistry
    {
        private readonly Dictionary<string, IMasterSource> _sources;

        public MasterRegistry(IEnumerable<IMasterSource> sources) =>
            _sources = sources.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

        public IMasterSource? Find(string key) =>
            _sources.TryGetValue(key, out var source) ? source : null;

        public IReadOnlyList<string> Keys => _sources.Keys.OrderBy(k => k).ToList();
    }
}
