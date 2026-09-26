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
    required super.isInstant,
    required super.isUrgent,
    required super.urgencyLabel,
    super.serviceItemName,
    super.distanceKm,
    super.distanceLabel,
    super.customerName,
    super.wasAssignedBySupport,
    super.assignmentNote,
  });

  factory GigTaskModel.fromJson(Map<String, dynamic> json) {
    return GigTaskModel(
      id: json['id'] as int,
      categoryId: json['categoryId'] as int,
      categoryName: json['categoryName'] as String? ?? '',
      serviceItemName: json['serviceItemName'] as String?,
      description: json['description'] as String? ?? '',
      address: json['address'] as String? ?? '',
      budget: (json['budget'] as num?)?.toDouble() ?? 0,
      effectiveAmount: (json['effectiveAmount'] as num?)?.toDouble() ?? 0,
      status: json['status'] as String? ?? '',
      statusLabel: json['statusLabel'] as String? ?? '',
      isInstant: json['isInstant'] as bool? ?? false,
      isUrgent: json['isUrgent'] as bool? ?? false,
      urgencyLabel: json['urgencyLabel'] as String? ?? '',
      distanceKm: (json['distanceKm'] as num?)?.toDouble(),
      distanceLabel: json['distanceLabel'] as String?,
      customerName: json['customerName'] as String?,
      wasAssignedBySupport: json['wasAssignedBySupport'] as bool? ?? false,
      assignmentNote: json['assignmentNote'] as String?,
    );
  }
}
