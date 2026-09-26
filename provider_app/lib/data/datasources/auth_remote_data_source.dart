import 'dart:io';

import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/app_user_model.dart';
import '../models/auth_response_model.dart';

class OtpVerifyResponse {
  const OtpVerifyResponse({required this.requiresPartnerRegistration, this.auth});

  final bool requiresPartnerRegistration;
  final AuthResponseModel? auth;
}

class AuthRemoteDataSource {
  AuthRemoteDataSource(this._dio);

  final Dio _dio;

  Future<String?> requestOtp(String phone, String role) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.otpRequest,
        data: {'phone': phone, 'role': role},
      );
      return response.data?['devCode'] as String?;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<OtpVerifyResponse> verifyOtp(String phone, String role, String code) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.otpVerify,
        data: {'phone': phone, 'role': role, 'code': code},
      );

      final body = response.data!;
      final requiresRegistration = body['requiresPartnerRegistration'] as bool? ?? false;
      final authJson = body['auth'] as Map<String, dynamic>?;

      return OtpVerifyResponse(
        requiresPartnerRegistration: requiresRegistration,
        auth: authJson == null ? null : AuthResponseModel.fromJson(authJson),
      );
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<AuthResponseModel> login(String identifier, String password, String role) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.login,
        data: {'identifier': identifier, 'password': password, 'role': role},
      );
      return AuthResponseModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<AuthResponseModel> registerPartner({
    required String name,
    required String phone,
    required String password,
    required bool phoneVerifiedViaOtp,
    required int skillCategoryId,
    required String aadhaarNumber,
    required File selfie,
    required File aadhaarFront,
    required File aadhaarBack,
    String? email,
  }) async {
    try {
      final formData = FormData.fromMap({
        'Name': name,
        'Phone': phone,
        'Password': password,
        'phoneVerifiedViaOtp': phoneVerifiedViaOtp,
        'SkillCategoryId': skillCategoryId,
        'AadhaarNumber': aadhaarNumber,
        if (email != null && email.isNotEmpty) 'Email': email,
        'Selfie': await MultipartFile.fromFile(selfie.path),
        'AadhaarFront': await MultipartFile.fromFile(aadhaarFront.path),
        'AadhaarBack': await MultipartFile.fromFile(aadhaarBack.path),
      });

      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.registerPartner,
        data: formData,
      );
      return AuthResponseModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<AppUserModel> me() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.me);
      return AppUserModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
