import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/app_user_model.dart';
import '../models/wallet_summary_model.dart';

class ProfileRemoteDataSource {
  ProfileRemoteDataSource(this._dio);

  final Dio _dio;

  Future<AppUserModel> updateProfile(Map<String, dynamic> body) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(ApiEndpoints.profile, data: body);
      return AppUserModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<BankAccountModel?> saveBankAccount(Map<String, dynamic> body) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(ApiEndpoints.profileBank, data: body);
      final data = response.data;
      return data == null ? null : BankAccountModel.fromJson(data);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> changePassword(Map<String, dynamic> body) async {
    try {
      await _dio.post<void>(ApiEndpoints.profilePassword, data: body);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
