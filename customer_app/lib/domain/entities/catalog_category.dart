class CatalogCategory {
  const CatalogCategory({
    required this.id,
    required this.name,
    required this.serviceCount,
    this.imageUrl,
    this.startingFrom,
  });

  final int id;
  final String name;
  final String? imageUrl;
  final int serviceCount;
  final double? startingFrom;
}
