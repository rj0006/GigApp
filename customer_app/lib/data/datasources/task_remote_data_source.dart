import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/gig_task_model.dart';

class TaskRemoteDataSource {
  TaskRemoteDataSource(this._dio);

  final Dio _dio;

  Future<List<GigTaskModel>> getMyTasks() async {
    try {
      final response = await _dio.get<List<dynamic>>(ApiEndpoints.myTasks);
      return response.data!
          .map((json) => GigTaskModel.fromJson(json as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<GigTaskModel> createTask(Map<String, dynamic> body) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(ApiEndpoints.createTask, data: body);
      return GigTaskModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<GigTaskModel> updateStatus(int id, Map<String, dynamic> body) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(ApiEndpoints.taskStatus(id), data: body);
      return GigTaskModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<GigTaskModel> rate(int id, Map<String, dynamic> body) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(ApiEndpoints.taskRate(id), data: body);
      return GigTaskModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<Map<String, dynamic>> raiseHelp(int taskId, Map<String, dynamic> body) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(ApiEndpoints.orderHelp(taskId), data: body);
      return response.data!;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<Map<String, dynamic>> getOrders({required int page, required int pageSize, String? status}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.orders,
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
          'status': ?status,
        },
      );
      return response.data!;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
