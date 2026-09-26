import 'dart:io';
import 'dart:math';

import '../../core/config/env.dart';
import '../../core/network/api_exception.dart';
import '../../core/storage/token_storage.dart';
import '../../domain/entities/app_user.dart';
import '../../domain/entities/otp_result.dart';
import '../../domain/repositories/auth_repository.dart';
import '../datasources/auth_remote_data_source.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this._remote, this._tokenStorage);

  final AuthRemoteDataSource _remote;
  final TokenStorage _tokenStorage;

  @override
  Future<String?> requestOtp(String phone) => _remote.requestOtp(phone, Env.role);

  @override
  Future<OtpVerifyResult> verifyOtp(String phone, String code) async {
    final response = await _remote.verifyOtp(phone, Env.role, code);

    if (response.requiresPartnerRegistration) {
      return const OtpVerifyRequiresRegistration();
    }

    final auth = response.auth;
    if (auth == null) {
      throw const ApiException('Could not verify that code.');
    }

    await _tokenStorage.save(auth.tokens);
    return OtpVerifySignedIn(auth.user);
  }

  @override
  Future<AppUser> signInWithPassword(String identifier, String password) async {
    final auth = await _remote.login(identifier, password, Env.role);
    await _tokenStorage.save(auth.tokens);
    return auth.user;
  }

  @override
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
  }) async {
    final auth = await _remote.registerPartner(
      name: name,
      phone: phone,
      password: _generatePassword(),
      phoneVerifiedViaOtp: phoneVerifiedViaOtp,
      skillCategoryId: skillCategoryId,
      aadhaarNumber: aadhaarNumber,
      selfie: selfie,
      aadhaarFront: aadhaarFront,
      aadhaarBack: aadhaarBack,
      email: email,
    );
    await _tokenStorage.save(auth.tokens);
    return auth.user;
  }

  String _generatePassword() {
    final random = Random.secure();
    final bytes = List<int>.generate(16, (_) => random.nextInt(256));
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex}Aa1!';
  }

  @override
  Future<AppUser?> restoreSession() async {
    final tokens = await _tokenStorage.read();
    if (tokens == null) return null;

    try {
      return await _remote.me();
    } on ApiException {
      await _tokenStorage.clear();
      return null;
    }
  }

  @override
  Future<void> logout() => _tokenStorage.clear();
}
