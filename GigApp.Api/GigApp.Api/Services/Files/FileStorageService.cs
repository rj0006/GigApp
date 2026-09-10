using Microsoft.Extensions.Options;

namespace GigApp.Api.Services.Files
{
    /// <summary>Where an upload belongs, and therefore who may read it back.</summary>
    public enum FileCategory
    {
        /// <summary>Served straight from wwwroot — customers see a partner's photo.</summary>
        ProfileImage,

        /// <summary>Public catalogue artwork, served from wwwroot like a profile photo.</summary>
        CategoryImage,

        ServiceImage,

        BannerImage,

        /// <summary>
        /// Identity documents. Stored outside wwwroot so a guessed URL cannot
        /// reach them; only <see cref="Controllers.FilesController"/> serves them.
        /// </summary>
        KycDocument,
    }

    public class FileStorageOptions
    {
        public const string SectionName = "FileStorage";

        public long MaxBytes { get; set; } = 5 * 1024 * 1024;   // 5 MB

        public static readonly string[] DefaultExtensions = { ".jpg", ".jpeg", ".jfif", ".png", ".webp" };

        // Binding an array onto a non-empty default appends to it, so a default
        // here would make an extension impossible to remove from configuration.
        public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> Extensions =>
            AllowedExtensions.Length > 0 ? AllowedExtensions : DefaultExtensions;

        /// <summary>Public folder, relative to wwwroot.</summary>
        public string ProfileFolder { get; set; } = "uploads/profile";

        /// <summary>Public folder for catalogue images, relative to wwwroot.</summary>
        public string CategoryFolder { get; set; } = "uploads/category";

        /// <summary>Public folder for service artwork, relative to wwwroot.</summary>
        public string ServiceFolder { get; set; } = "uploads/service";

        /// <summary>Public folder for promotional artwork, relative to wwwroot.</summary>
        public string BannerFolder { get; set; } = "uploads/banner";

        /// <summary>Private folder, relative to the content root (NOT wwwroot).</summary>
        public string KycFolder { get; set; } = "App_Data/kyc";
    }

    public class FileSaveResult
    {
        public bool Succeeded { get; init; }
        public string? FileName { get; init; }
        public string? Error { get; init; }

        public static FileSaveResult Ok(string fileName) => new() { Succeeded = true, FileName = fileName };
        public static FileSaveResult Fail(string error) => new() { Succeeded = false, Error = error };
    }

    public interface IFileStorageService
    {
        Task<FileSaveResult> SaveAsync(IFormFile? file, FileCategory category, CancellationToken ct = default);

        /// <summary>Absolute path, or null when the file is missing or the name is unsafe.</summary>
        string? ResolvePath(string? fileName, FileCategory category);

        void Delete(string? fileName, FileCategory category);

        /// <summary>Browser-reachable URL for a stored file.</summary>
        string? PublicUrl(string? fileName, FileCategory category);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly FileStorageOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(
            IOptions<FileStorageOptions> options,
            IWebHostEnvironment environment,
            ILogger<FileStorageService> logger)
        {
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task<FileSaveResult> SaveAsync(
            IFormFile? file, FileCategory category, CancellationToken ct = default)
        {
            if (file is null || file.Length == 0)
                return FileSaveResult.Fail("Choose a file to upload.");

            if (file.Length > _options.MaxBytes)
                return FileSaveResult.Fail($"File is larger than {_options.MaxBytes / (1024 * 1024)} MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!_options.Extensions.Contains(extension))
                return FileSaveResult.Fail(
                    $"Only {string.Join(", ", _options.Extensions)} files are allowed.");

            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return FileSaveResult.Fail("Only image files are allowed.");

            // An attacker controls both the extension and the content type, so
            // check the actual bytes before trusting either.
            if (!await HasImageSignatureAsync(file, ct))
                return FileSaveResult.Fail("That file is not a valid image.");

            // Rename on the way in: the original name is attacker-controlled and
            // could collide, contain a path, or leak the uploader's details.
            var storedName = $"{Guid.NewGuid():N}{extension}";
            var directory = DirectoryFor(category);

            Directory.CreateDirectory(directory);

            var fullPath = Path.Combine(directory, storedName);

            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream, ct);
            }

            _logger.LogInformation("Stored {Category} upload as {FileName}", category, storedName);

            return FileSaveResult.Ok(storedName);
        }

        public string? ResolvePath(string? fileName, FileCategory category)
        {
            if (!IsSafeName(fileName)) return null;

            var fullPath = Path.Combine(DirectoryFor(category), fileName!);
            return File.Exists(fullPath) ? fullPath : null;
        }

        public void Delete(string? fileName, FileCategory category)
        {
            var path = ResolvePath(fileName, category);
            if (path is null) return;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                // A stale file on disk is not worth failing the user's action for.
                _logger.LogWarning(ex, "Could not delete {FileName}", fileName);
            }
        }

        public string? PublicUrl(string? fileName, FileCategory category)
        {
            if (!IsSafeName(fileName)) return null;

            return category switch
            {
                FileCategory.ProfileImage => $"/{_options.ProfileFolder}/{fileName}",
                FileCategory.CategoryImage => $"/{_options.CategoryFolder}/{fileName}",
                FileCategory.ServiceImage => $"/{_options.ServiceFolder}/{fileName}",
                FileCategory.BannerImage => $"/{_options.BannerFolder}/{fileName}",
                FileCategory.KycDocument => $"/api/files/kyc/{fileName}",
                _ => null,
            };
        }

        private string DirectoryFor(FileCategory category) => category switch
        {
            FileCategory.ProfileImage =>
                Path.Combine(_environment.WebRootPath ?? "wwwroot", _options.ProfileFolder),

            FileCategory.CategoryImage =>
                Path.Combine(_environment.WebRootPath ?? "wwwroot", _options.CategoryFolder),

            FileCategory.ServiceImage =>
                Path.Combine(_environment.WebRootPath ?? "wwwroot", _options.ServiceFolder),

            FileCategory.BannerImage =>
                Path.Combine(_environment.WebRootPath ?? "wwwroot", _options.BannerFolder),

            // Content root, not web root — this must not be publicly served.
            FileCategory.KycDocument =>
                Path.Combine(_environment.ContentRootPath, _options.KycFolder),

            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        /// <summary>
        /// Stored names are always "guid.ext". Rejecting anything else stops a
        /// crafted name like "../../appsettings.json" from escaping the folder.
        /// </summary>
        private static bool IsSafeName(string? fileName) =>
            !string.IsNullOrWhiteSpace(fileName)
            && fileName == Path.GetFileName(fileName)
            && !fileName.Contains("..", StringComparison.Ordinal);

        private static async Task<bool> HasImageSignatureAsync(IFormFile file, CancellationToken ct)
        {
            var header = new byte[12];

            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(), ct);
            if (read < 4) return false;

            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;

            // WEBP: "RIFF" .... "WEBP"
            if (read >= 12
                && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return true;

            return false;
        }
    }
}
