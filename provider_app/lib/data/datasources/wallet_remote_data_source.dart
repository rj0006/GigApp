import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/wallet_summary_model.dart';

class WalletRemoteDataSource {
  WalletRemoteDataSource(this._dio);

  final Dio _dio;

  Future<WalletSummaryModel> getSummary() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.myEarnings);
      return WalletSummaryModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<Map<String, dynamic>> getEntries({required int page, required int pageSize}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.myEarningsEntries,
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return response.data!;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
