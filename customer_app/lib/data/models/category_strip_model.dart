import '../../domain/entities/category_strip.dart';
import 'service_item_model.dart';

class CategoryStripModel extends CategoryStrip {
  const CategoryStripModel({
    required super.categoryId,
    required super.categoryName,
    required super.totalCount,
    required super.services,
  });

  factory CategoryStripModel.fromJson(Map<String, dynamic> json) {
    final category = json['category'] as Map<String, dynamic>? ?? const {};
    final services = json['services'] as List<dynamic>? ?? const [];
    return CategoryStripModel(
      categoryId: category['id'] as int? ?? 0,
      categoryName: category['name'] as String? ?? '',
      totalCount: json['totalCount'] as int? ?? 0,
      services: services.map((e) => ServiceItemModel.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }
}
