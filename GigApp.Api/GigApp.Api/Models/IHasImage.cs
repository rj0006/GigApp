namespace GigApp.Api.Models
{
    /// <summary>
    /// A master row that carries one picture for the public catalogue. The admin
    /// controller saves and replaces it through a single helper, so every one of
    /// them validates and cleans up the same way.
    /// </summary>
    public interface IHasImage
    {
        string? ImageFileName { get; set; }
    }
}
