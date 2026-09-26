class CatalogBanner {
  const CatalogBanner({
    required this.id,
    required this.title,
    required this.placement,
    this.subtitle,
    this.callToAction,
    this.imageUrl,
    this.linkUrl,
  });

  final int id;
  final String title;
  final String? subtitle;
  final String? callToAction;
  final String? imageUrl;
  final String? linkUrl;
  final String placement;
}
