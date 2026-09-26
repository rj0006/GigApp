import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/app_notification_model.dart';

class NotificationRemoteDataSource {
  NotificationRemoteDataSource(this._dio);

  final Dio _dio;

  Future<NotificationSummaryModel> getSummary() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.notificationSummary);
      return NotificationSummaryModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<Map<String, dynamic>> getList({required int page, required int pageSize}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.notifications,
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return response.data!;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> markRead({int? notificationId}) async {
    try {
      await _dio.post<void>(
        ApiEndpoints.notificationsRead,
        data: notificationId == null ? null : {'notificationId': notificationId},
      );
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
