import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../auth/session_controller.dart';
import '../providers.dart';
import 'partner_registration_state.dart';

final _aadhaarPattern = RegExp(r'^[2-9]\d{11}$');

class PartnerRegistrationController extends StateNotifier<PartnerRegistrationState> {
  PartnerRegistrationController(this._ref) : super(const PartnerRegistrationState());

  final Ref _ref;

  void setName(String value) => state = state.copyWith(name: value);

  void setEmail(String value) => state = state.copyWith(email: value);

  void setSkillCategoryId(int id) => state = state.copyWith(skillCategoryId: id);

  void setAadhaarNumber(String value) => state = state.copyWith(aadhaarNumber: value);

  void setSelfie(File file) => state = state.copyWith(selfie: file);

  void setAadhaarFront(File file) => state = state.copyWith(aadhaarFront: file);

  void setAadhaarBack(File file) => state = state.copyWith(aadhaarBack: file);

  Future<void> submit(String phone) async {
    final validationError = _validate();
    if (validationError != null) {
      state = state.copyWith(error: validationError);
      return;
    }

    state = state.copyWith(isSubmitting: true, clearError: true);

    try {
      final user = await _ref.read(authRepositoryProvider).registerPartner(
            name: state.name.trim(),
            phone: phone,
            phoneVerifiedViaOtp: true,
            skillCategoryId: state.skillCategoryId!,
            aadhaarNumber: state.aadhaarNumber,
            selfie: state.selfie!,
            aadhaarFront: state.aadhaarFront!,
            aadhaarBack: state.aadhaarBack!,
            email: state.email,
          );
      state = state.copyWith(isSubmitting: false);
      _ref.read(sessionControllerProvider.notifier).setSignedIn(user);
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, error: e.message);
    }
  }

  String? _validate() {
    if (state.name.trim().length < 2) return 'Enter your full name.';
    if (state.skillCategoryId == null) return 'Choose a skill category.';
    if (state.selfie == null) return 'Add your selfie.';
    if (state.aadhaarFront == null) return 'Add the front of your Aadhaar card.';
    if (state.aadhaarBack == null) return 'Add the back of your Aadhaar card.';
    if (!_aadhaarPattern.hasMatch(state.aadhaarNumber)) return 'Enter a valid 12-digit Aadhaar number.';
    return null;
  }
}

final partnerRegistrationControllerProvider =
    StateNotifierProvider.autoDispose<PartnerRegistrationController, PartnerRegistrationState>((ref) {
  return PartnerRegistrationController(ref);
});
