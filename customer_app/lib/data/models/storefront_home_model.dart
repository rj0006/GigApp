import '../../domain/entities/storefront_home.dart';
import 'catalog_banner_model.dart';
import 'catalog_category_model.dart';
import 'category_strip_model.dart';
import 'service_item_model.dart';
import 'storefront_stats_model.dart';

class StorefrontHomeModel extends StorefrontHome {
  const StorefrontHomeModel({
    required super.categories,
    required super.popular,
    required super.newAndNoteworthy,
    required super.strips,
    required super.spotlight,
    required super.stats,
    super.wideBanner,
  });

  factory StorefrontHomeModel.fromJson(Map<String, dynamic> json) {
    final categories = json['categories'] as List<dynamic>? ?? const [];
    final popular = json['popular'] as List<dynamic>? ?? const [];
    final fresh = json['newAndNoteworthy'] as List<dynamic>? ?? const [];
    final strips = json['strips'] as List<dynamic>? ?? const [];
    final spotlight = json['spotlight'] as List<dynamic>? ?? const [];
    final wideBanner = json['wideBanner'] as Map<String, dynamic>?;

    return StorefrontHomeModel(
      categories: categories.map((e) => CatalogCategoryModel.fromJson(e as Map<String, dynamic>)).toList(),
      popular: popular.map((e) => ServiceItemModel.fromJson(e as Map<String, dynamic>)).toList(),
      newAndNoteworthy: fresh.map((e) => ServiceItemModel.fromJson(e as Map<String, dynamic>)).toList(),
      strips: strips.map((e) => CategoryStripModel.fromJson(e as Map<String, dynamic>)).toList(),
      spotlight: spotlight.map((e) => CatalogBannerModel.fromJson(e as Map<String, dynamic>)).toList(),
      wideBanner: wideBanner == null ? null : CatalogBannerModel.fromJson(wideBanner),
      stats: StorefrontStatsModel.fromJson(json['stats'] as Map<String, dynamic>? ?? const {}),
    );
  }
}
