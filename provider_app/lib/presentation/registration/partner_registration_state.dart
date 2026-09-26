import 'dart:io';

class PartnerRegistrationState {
  const PartnerRegistrationState({
    this.name = '',
    this.email,
    this.skillCategoryId,
    this.aadhaarNumber = '',
    this.selfie,
    this.aadhaarFront,
    this.aadhaarBack,
    this.isSubmitting = false,
    this.error,
  });

  final String name;
  final String? email;
  final int? skillCategoryId;
  final String aadhaarNumber;
  final File? selfie;
  final File? aadhaarFront;
  final File? aadhaarBack;
  final bool isSubmitting;
  final String? error;

  PartnerRegistrationState copyWith({
    String? name,
    String? email,
    int? skillCategoryId,
    String? aadhaarNumber,
    File? selfie,
    File? aadhaarFront,
    File? aadhaarBack,
    bool? isSubmitting,
    String? error,
    bool clearError = false,
  }) {
    return PartnerRegistrationState(
      name: name ?? this.name,
      email: email ?? this.email,
      skillCategoryId: skillCategoryId ?? this.skillCategoryId,
      aadhaarNumber: aadhaarNumber ?? this.aadhaarNumber,
      selfie: selfie ?? this.selfie,
      aadhaarFront: aadhaarFront ?? this.aadhaarFront,
      aadhaarBack: aadhaarBack ?? this.aadhaarBack,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      error: clearError ? null : (error ?? this.error),
    );
  }
}
