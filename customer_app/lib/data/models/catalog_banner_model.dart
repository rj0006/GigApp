import '../../core/utils/image_url.dart';
import '../../domain/entities/catalog_banner.dart';

class CatalogBannerModel extends CatalogBanner {
  const CatalogBannerModel({
    required super.id,
    required super.title,
    required super.placement,
    super.subtitle,
    super.callToAction,
    super.imageUrl,
    super.linkUrl,
  });

  factory CatalogBannerModel.fromJson(Map<String, dynamic> json) {
    return CatalogBannerModel(
      id: json['id'] as int,
      title: json['title'] as String? ?? '',
      subtitle: json['subtitle'] as String?,
      callToAction: json['callToAction'] as String?,
      imageUrl: resolveImageUrl(json['imageUrl'] as String?),
      linkUrl: json['linkUrl'] as String?,
      placement: json['placement'] as String? ?? 'spotlight',
    );
  }
}
