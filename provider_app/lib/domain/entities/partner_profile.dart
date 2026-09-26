class PartnerSummary {
  const PartnerSummary({
    required this.id,
    required this.skillCategoryId,
    required this.skillCategoryName,
    required this.kycStatus,
    required this.isVerified,
    required this.isAvailable,
    required this.hasCompleteKyc,
  });

  final int id;
  final int skillCategoryId;
  final String skillCategoryName;
  final String kycStatus;
  final bool isVerified;
  final bool isAvailable;
  final bool hasCompleteKyc;
}
