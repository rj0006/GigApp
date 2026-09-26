import '../../core/utils/image_url.dart';
import '../../domain/entities/catalog_category.dart';

class CatalogCategoryModel extends CatalogCategory {
  const CatalogCategoryModel({
    required super.id,
    required super.name,
    required super.serviceCount,
    super.imageUrl,
    super.startingFrom,
  });

  factory CatalogCategoryModel.fromJson(Map<String, dynamic> json) {
    final category = json['category'] as Map<String, dynamic>? ?? const {};
    return CatalogCategoryModel(
      id: category['id'] as int? ?? 0,
      name: category['name'] as String? ?? '',
      imageUrl: resolveImageUrl(category['imageUrl'] as String?),
      serviceCount: json['serviceCount'] as int? ?? 0,
      startingFrom: (json['startingFrom'] as num?)?.toDouble(),
    );
  }
}
