import '../../domain/entities/gig_task.dart';

class GigTaskModel extends GigTask {
  const GigTaskModel({
    required super.id,
    required super.categoryId,
    required super.categoryName,
    required super.description,
    required super.address,
    required super.budget,
    required super.effectiveAmount,
    required super.status,
    required super.statusLabel,
    required super.urgency,
    required super.urgencyLabel,
    required super.isInstant,
    required super.createdAt,
    super.serviceItemId,
    super.serviceItemName,
    super.agreedAmount,
    super.addressId,
    super.preferredDateTime,
    super.completedAt,
    super.partnerId,
    super.partnerName,
    super.partnerSkillCategory,
  });

  factory GigTaskModel.fromJson(Map<String, dynamic> json) {
    return GigTaskModel(
      id: json['id'] as int,
      categoryId: json['categoryId'] as int? ?? 0,
      categoryName: json['categoryName'] as String? ?? '',
      serviceItemId: json['serviceItemId'] as int?,
      serviceItemName: json['serviceItemName'] as String?,
      description: json['description'] as String? ?? '',
      address: json['address'] as String? ?? '',
      addressId: json['addressId'] as int?,
      budget: (json['budget'] as num?)?.toDouble() ?? 0,
      agreedAmount: (json['agreedAmount'] as num?)?.toDouble(),
      effectiveAmount: (json['effectiveAmount'] as num?)?.toDouble() ?? 0,
      status: json['status'] as String? ?? '',
      statusLabel: json['statusLabel'] as String? ?? '',
      urgency: json['urgency'] as String? ?? 'normal',
      urgencyLabel: json['urgencyLabel'] as String? ?? '',
      isInstant: json['isInstant'] as bool? ?? false,
      preferredDateTime:
          json['preferredDateTime'] == null ? null : DateTime.parse(json['preferredDateTime'] as String),
      createdAt: DateTime.parse(json['createdAt'] as String),
      completedAt: json['completedAt'] == null ? null : DateTime.parse(json['completedAt'] as String),
      partnerId: json['partnerId'] as int?,
      partnerName: json['partnerName'] as String?,
      partnerSkillCategory: json['partnerSkillCategory'] as String?,
    );
  }
}
