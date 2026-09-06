using GigApp.Api.Dtos;

namespace GigApp.Api.ViewModels
{
    public class QuantityStepperModel
    {
        public int ServiceItemId { get; set; }
        public int Quantity { get; set; }
        public string? ReturnTo { get; set; }
        public int Maximum { get; set; } = 20;
    }

    public class CategoryStripViewModel
    {
        public SkillCategoryDto Category { get; set; } = new();
        public IReadOnlyList<ServiceItemDto> Services { get; set; } = Array.Empty<ServiceItemDto>();
        public int TotalCount { get; set; }

        public bool HasMore => TotalCount > Services.Count;
    }

    public class StorefrontStatsViewModel
    {
        public decimal? AverageRating { get; set; }
        public int RatingCount { get; set; }
        public int CompletedCount { get; set; }
        public int PartnerCount { get; set; }

        /// <summary>Hide the strip entirely until the numbers mean something.</summary>
        public bool IsWorthShowing => RatingCount >= 5 || CompletedCount >= 20;
    }

    public class StorefrontViewModel
    {
        public IReadOnlyList<CatalogCategoryViewModel> Categories { get; set; } =
            Array.Empty<CatalogCategoryViewModel>();

        public IReadOnlyList<ServiceItemDto> Popular { get; set; } =
            Array.Empty<ServiceItemDto>();

        public IReadOnlyList<ServiceItemDto> NewAndNoteworthy { get; set; } =
            Array.Empty<ServiceItemDto>();

        public IReadOnlyList<CategoryStripViewModel> Strips { get; set; } =
            Array.Empty<CategoryStripViewModel>();

        public IReadOnlyList<BannerDto> Spotlight { get; set; } = Array.Empty<BannerDto>();
        public BannerDto? WideBanner { get; set; }

        public StorefrontStatsViewModel Stats { get; set; } = new();
        public CartViewDto Cart { get; set; } = new();

        public int QuantityOf(int serviceItemId) =>
            Cart.Lines.FirstOrDefault(l => l.ServiceItemId == serviceItemId)?.Quantity ?? 0;
    }

    public class StorefrontSearchViewModel
    {
        public string Term { get; set; } = string.Empty;
        public IReadOnlyList<ServiceItemDto> Results { get; set; } = Array.Empty<ServiceItemDto>();

        public IReadOnlyList<CatalogCategoryViewModel> Categories { get; set; } =
            Array.Empty<CatalogCategoryViewModel>();

        public CartViewDto Cart { get; set; } = new();

        public int QuantityOf(int serviceItemId) =>
            Cart.Lines.FirstOrDefault(l => l.ServiceItemId == serviceItemId)?.Quantity ?? 0;
    }

    public class StorefrontCategoryViewModel
    {
        public SkillCategoryDto Category { get; set; } = new();
        public IReadOnlyList<ServiceItemDto> Services { get; set; } = Array.Empty<ServiceItemDto>();

        public IReadOnlyList<CatalogCategoryViewModel> Categories { get; set; } =
            Array.Empty<CatalogCategoryViewModel>();

        public CartViewDto Cart { get; set; } = new();

        public int QuantityOf(int serviceItemId) =>
            Cart.Lines.FirstOrDefault(l => l.ServiceItemId == serviceItemId)?.Quantity ?? 0;
    }

    public class StorefrontCartViewModel
    {
        public CartViewDto Cart { get; set; } = new();

        public IReadOnlyList<CatalogCategoryViewModel> Categories { get; set; } =
            Array.Empty<CatalogCategoryViewModel>();
    }

    public class StorefrontCheckoutViewModel
    {
        public CartViewDto Cart { get; set; } = new();
        public bool IsSignedIn { get; set; }
        public IReadOnlyList<AddressDto> Addresses { get; set; } = Array.Empty<AddressDto>();

        public bool NeedsAddress => IsSignedIn && Addresses.Count == 0;
    }
}
