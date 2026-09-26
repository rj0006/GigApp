import '../../domain/entities/partner_profile.dart';

class PartnerSummaryModel extends PartnerSummary {
  const PartnerSummaryModel({
    required super.id,
    required super.skillCategoryId,
    required super.skillCategoryName,
    required super.kycStatus,
    required super.isVerified,
    required super.isAvailable,
    required super.hasCompleteKyc,
  });

  factory PartnerSummaryModel.fromJson(Map<String, dynamic> json) {
    return PartnerSummaryModel(
      id: json['id'] as int,
      skillCategoryId: json['skillCategoryId'] as int,
      skillCategoryName: json['skillCategoryName'] as String? ?? '',
      kycStatus: json['kycStatus'] as String? ?? 'not_submitted',
      isVerified: json['isVerified'] as bool? ?? false,
      isAvailable: json['isAvailable'] as bool? ?? false,
      hasCompleteKyc: json['hasCompleteKyc'] as bool? ?? false,
    );
  }
}
