import '../../core/utils/image_url.dart';
import '../../domain/entities/service_item.dart';

class ServiceItemModel extends ServiceItem {
  const ServiceItemModel({
    required super.id,
    required super.skillCategoryId,
    required super.name,
    required super.allowsInstantBooking,
    super.description,
    super.imageUrl,
    super.basePayout,
  });

  factory ServiceItemModel.fromJson(Map<String, dynamic> json) {
    return ServiceItemModel(
      id: json['id'] as int,
      skillCategoryId: json['skillCategoryId'] as int? ?? 0,
      name: json['name'] as String? ?? '',
      description: json['description'] as String?,
      imageUrl: resolveImageUrl(json['imageUrl'] as String?),
      basePayout: (json['basePayout'] as num?)?.toDouble(),
      allowsInstantBooking: json['allowsInstantBooking'] as bool? ?? false,
    );
  }
}
