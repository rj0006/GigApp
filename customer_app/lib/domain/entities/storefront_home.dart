import 'catalog_banner.dart';
import 'catalog_category.dart';
import 'category_strip.dart';
import 'service_item.dart';
import 'storefront_stats.dart';

class StorefrontHome {
  const StorefrontHome({
    required this.categories,
    required this.popular,
    required this.newAndNoteworthy,
    required this.strips,
    required this.spotlight,
    required this.stats,
    this.wideBanner,
  });

  final List<CatalogCategory> categories;
  final List<ServiceItem> popular;
  final List<ServiceItem> newAndNoteworthy;
  final List<CategoryStrip> strips;
  final List<CatalogBanner> spotlight;
  final CatalogBanner? wideBanner;
  final StorefrontStats stats;
}
