import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';

class TaskRemoteDataSource {
  TaskRemoteDataSource(this._dio);

  final Dio _dio;

  Future<void> acceptInstantTask(int id) async {
    try {
      await _dio.put<void>(ApiEndpoints.taskAccept(id));
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> updateStatus(
    int id, {
    required String status,
    int? stars,
    String? feedback,
    String? cancelReason,
  }) async {
    try {
      await _dio.put<void>(
        ApiEndpoints.taskStatus(id),
        data: {
          'status': status,
          'stars': ?stars,
          if (feedback != null && feedback.isNotEmpty) 'feedback': feedback,
          if (cancelReason != null && cancelReason.isNotEmpty) 'cancelReason': cancelReason,
        },
      );
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
