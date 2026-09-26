import 'dart:io';

import '../entities/app_user.dart';
import '../entities/otp_result.dart';

abstract class AuthRepository {
  Future<String?> requestOtp(String phone);

  Future<OtpVerifyResult> verifyOtp(String phone, String code);

  Future<AppUser> signInWithPassword(String identifier, String password);

  Future<AppUser> registerPartner({
    required String name,
    required String phone,
    required bool phoneVerifiedViaOtp,
    required int skillCategoryId,
    required String aadhaarNumber,
    required File selfie,
    required File aadhaarFront,
    required File aadhaarBack,
    String? email,
  });

  Future<AppUser?> restoreSession();

  Future<void> logout();
}
