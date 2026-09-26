class GigTask {
  const GigTask({
    required this.id,
    required this.categoryId,
    required this.categoryName,
    required this.description,
    required this.address,
    required this.budget,
    required this.effectiveAmount,
    required this.status,
    required this.statusLabel,
    required this.urgency,
    required this.urgencyLabel,
    required this.isInstant,
    required this.createdAt,
    this.serviceItemId,
    this.serviceItemName,
    this.agreedAmount,
    this.addressId,
    this.preferredDateTime,
    this.completedAt,
    this.partnerId,
    this.partnerName,
    this.partnerSkillCategory,
  });

  final int id;
  final int categoryId;
  final String categoryName;
  final int? serviceItemId;
  final String? serviceItemName;
  final String description;
  final String address;
  final int? addressId;
  final double budget;
  final double? agreedAmount;
  final double effectiveAmount;
  final String status;
  final String statusLabel;
  final String urgency;
  final String urgencyLabel;
  final bool isInstant;
  final DateTime? preferredDateTime;
  final DateTime createdAt;
  final DateTime? completedAt;
  final int? partnerId;
  final String? partnerName;
  final String? partnerSkillCategory;
}
