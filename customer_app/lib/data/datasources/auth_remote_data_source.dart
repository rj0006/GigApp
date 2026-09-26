import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/app_user_model.dart';
import '../models/auth_response_model.dart';

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

  Future<AuthResponseModel> verifyOtp(String phone, String role, String code, String? name) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.otpVerify,
        data: {
          'phone': phone,
          'role': role,
          'code': code,
          if (name != null && name.isNotEmpty) 'name': name,
        },
      );

      final body = response.data!;
      final authJson = body['auth'] as Map<String, dynamic>?;

      if (authJson == null) {
        throw const ApiException('Could not verify that code.');
      }

      return AuthResponseModel.fromJson(authJson);
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

  Future<AppUserModel> me() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.me);
      return AppUserModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
