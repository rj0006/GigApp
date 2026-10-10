import '../entities/app_user.dart';
import '../entities/otp_request_outcome.dart';

abstract class AuthRepository {
  Future<OtpRequestOutcome> requestOtp(String phone);

  Future<AppUser> verifyOtp(String phone, String code, {String? name});

  Future<AppUser> signInWithPassword(String identifier, String password);

  Future<AppUser?> restoreSession();

  Future<void> logout();
}
