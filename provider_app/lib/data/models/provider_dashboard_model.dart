import '../../domain/entities/provider_dashboard.dart';
import 'bid_model.dart';
import 'gig_task_model.dart';
import 'task_offer_model.dart';

class DashboardPartnerProfileModel extends DashboardPartnerProfile {
  const DashboardPartnerProfileModel({
    required super.name,
    required super.skillCategoryId,
    required super.skillCategoryName,
    required super.kycStatus,
    required super.kycLabel,
    required super.isVerified,
    required super.isAvailable,
    super.kycRejectionReason,
    super.averageRating,
    super.ratingCount,
  });

  factory DashboardPartnerProfileModel.fromJson(Map<String, dynamic> json) {
    return DashboardPartnerProfileModel(
      name: json['name'] as String? ?? '',
      skillCategoryId: json['skillCategoryId'] as int? ?? 0,
      skillCategoryName: json['skillCategoryName'] as String? ?? '',
      kycStatus: json['kycStatus'] as String? ?? 'not_submitted',
      kycLabel: json['kycLabel'] as String? ?? '',
      isVerified: json['isVerified'] as bool? ?? false,
      isAvailable: json['isAvailable'] as bool? ?? false,
      kycRejectionReason: json['kycRejectionReason'] as String?,
      averageRating: (json['averageRating'] as num?)?.toDouble(),
      ratingCount: json['ratingCount'] as int? ?? 0,
    );
  }
}

class ProviderDashboardModel extends ProviderDashboard {
  const ProviderDashboardModel({
    required super.profile,
    required super.availableTasks,
    required super.myBids,
    required super.myJobs,
    required super.hasServiceArea,
    required super.serviceRadiusKm,
    super.offer,
  });

  factory ProviderDashboardModel.fromJson(Map<String, dynamic> json) {
    final profileJson = json['profile'] as Map<String, dynamic>?;

    return ProviderDashboardModel(
      profile: profileJson == null ? null : DashboardPartnerProfileModel.fromJson(profileJson),
      availableTasks: ((json['availableTasks'] as List?) ?? [])
          .map((e) => GigTaskModel.fromJson(e as Map<String, dynamic>))
          .toList(),
      myBids: ((json['myBids'] as List?) ?? [])
          .map((e) => BidModel.fromJson(e as Map<String, dynamic>))
          .toList(),
      myJobs: ((json['myJobs'] as List?) ?? [])
          .map((e) => GigTaskModel.fromJson(e as Map<String, dynamic>))
          .toList(),
      hasServiceArea: json['hasServiceArea'] as bool? ?? false,
      serviceRadiusKm: json['serviceRadiusKm'] as int? ?? 0,
      offer: json['offer'] == null ? null : TaskOfferModel.fromJson(json['offer'] as Map<String, dynamic>),
    );
  }
}
