import '../entities/app_user.dart';

abstract class AuthRepository {
  Future<String?> requestOtp(String phone);

  Future<AppUser> verifyOtp(String phone, String code, {String? name});

  Future<AppUser> signInWithPassword(String identifier, String password);

  Future<AppUser?> restoreSession();

  Future<void> logout();
}
