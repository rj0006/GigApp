import 'service_item.dart';

class CategoryStrip {
  const CategoryStrip({
    required this.categoryId,
    required this.categoryName,
    required this.totalCount,
    required this.services,
  });

  final int categoryId;
  final String categoryName;
  final int totalCount;
  final List<ServiceItem> services;

  bool get hasMore => totalCount > services.length;
}
