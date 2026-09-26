import '../../domain/entities/app_user.dart';
import 'partner_summary_model.dart';

class AppUserModel extends AppUser {
  const AppUserModel({
    required super.id,
    required super.name,
    required super.phone,
    required super.role,
    required super.isPhoneVerified,
    required super.isActive,
    super.email,
    super.profileImageUrl,
    super.partnerProfile,
  });

  factory AppUserModel.fromJson(Map<String, dynamic> json) {
    final partnerJson = json['partnerProfile'] as Map<String, dynamic>?;

    return AppUserModel(
      id: json['id'] as int,
      name: json['name'] as String? ?? '',
      phone: json['phone'] as String? ?? '',
      email: json['email'] as String?,
      role: json['role'] as String? ?? '',
      isPhoneVerified: json['isPhoneVerified'] as bool? ?? false,
      isActive: json['isActive'] as bool? ?? true,
      profileImageUrl: json['profileImageUrl'] as String?,
      partnerProfile:
          partnerJson == null ? null : PartnerSummaryModel.fromJson(partnerJson),
    );
  }
}
