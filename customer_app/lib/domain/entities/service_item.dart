class ServiceItem {
  const ServiceItem({
    required this.id,
    required this.skillCategoryId,
    required this.name,
    required this.allowsInstantBooking,
    this.description,
    this.imageUrl,
    this.basePayout,
  });

  final int id;
  final int skillCategoryId;
  final String name;
  final String? description;
  final String? imageUrl;
  final double? basePayout;
  final bool allowsInstantBooking;
}
