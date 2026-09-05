namespace GigApp.Api.ViewModels
{
    public static class ImageUploadShape
    {
        public const string Wide = "wide";
        public const string Square = "square";
        public const string Circle = "circle";
    }

    public class ImageUploadModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Hint { get; set; }
        public string? CurrentUrl { get; set; }

        public bool Required { get; set; }
        public string Shape { get; set; } = ImageUploadShape.Wide;

        public bool AllowCamera { get; set; }

        public int MaxPixels { get; set; } = 1600;
        public long MaxBytes { get; set; } = 5 * 1024 * 1024;

        public string FieldId => Id ?? Name;
        public string MaxLabel => $"{MaxBytes / (1024 * 1024)} MB";
    }
}
