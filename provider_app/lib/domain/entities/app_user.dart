import 'partner_profile.dart';

class AppUser {
  const AppUser({
    required this.id,
    required this.name,
    required this.phone,
    required this.role,
    required this.isPhoneVerified,
    required this.isActive,
    this.email,
    this.profileImageUrl,
    this.partnerProfile,
  });

  final int id;
  final String name;
  final String phone;
  final String? email;
  final String role;
  final bool isPhoneVerified;
  final bool isActive;
  final String? profileImageUrl;
  final PartnerSummary? partnerProfile;
}
