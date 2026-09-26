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
    required this.isInstant,
    required this.isUrgent,
    required this.urgencyLabel,
    this.serviceItemName,
    this.distanceKm,
    this.distanceLabel,
    this.customerName,
    this.wasAssignedBySupport = false,
    this.assignmentNote,
  });

  final int id;
  final int categoryId;
  final String categoryName;
  final String? serviceItemName;
  final String description;
  final String address;
  final double budget;
  final double effectiveAmount;
  final String status;
  final String statusLabel;
  final bool isInstant;
  final bool isUrgent;
  final String urgencyLabel;
  final double? distanceKm;
  final String? distanceLabel;
  final String? customerName;
  final bool wasAssignedBySupport;
  final String? assignmentNote;
}
