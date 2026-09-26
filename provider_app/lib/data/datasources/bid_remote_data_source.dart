import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';

class BidRemoteDataSource {
  BidRemoteDataSource(this._dio);

  final Dio _dio;

  Future<void> placeBid({required int taskId, required double amount, String? note}) async {
    try {
      await _dio.post<void>(
        ApiEndpoints.bidsForTask(taskId),
        data: {'amount': amount, if (note != null && note.isNotEmpty) 'note': note},
      );
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> withdrawBid(int bidId) async {
    try {
      await _dio.post<void>(ApiEndpoints.bidWithdraw(bidId));
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> acceptCounter(int bidId) async {
    try {
      await _dio.post<void>(ApiEndpoints.bidAcceptCounter(bidId));
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
