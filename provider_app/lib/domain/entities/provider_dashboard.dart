import 'bid.dart';
import 'gig_task.dart';
import 'task_offer.dart';

class DashboardPartnerProfile {
  const DashboardPartnerProfile({
    required this.name,
    required this.skillCategoryId,
    required this.skillCategoryName,
    required this.kycStatus,
    required this.kycLabel,
    required this.isVerified,
    required this.isAvailable,
    this.kycRejectionReason,
    this.averageRating,
    this.ratingCount = 0,
  });

  final String name;
  final int skillCategoryId;
  final String skillCategoryName;
  final String kycStatus;
  final String kycLabel;
  final bool isVerified;
  final bool isAvailable;
  final String? kycRejectionReason;
  final double? averageRating;
  final int ratingCount;
}

class ProviderDashboard {
  const ProviderDashboard({
    required this.profile,
    required this.availableTasks,
    required this.myBids,
    required this.myJobs,
    required this.hasServiceArea,
    required this.serviceRadiusKm,
    this.offer,
  });

  final DashboardPartnerProfile? profile;
  final List<GigTask> availableTasks;
  final List<Bid> myBids;
  final List<GigTask> myJobs;
  final bool hasServiceArea;
  final int serviceRadiusKm;
  final TaskOffer? offer;
}
