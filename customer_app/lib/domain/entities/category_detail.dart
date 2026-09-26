import 'service_item.dart';

class CategoryDetail {
  const CategoryDetail({required this.categoryName, required this.services});

  final String categoryName;
  final List<ServiceItem> services;
}
