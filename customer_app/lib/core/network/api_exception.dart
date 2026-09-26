import 'package:dio/dio.dart';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  factory ApiException.fromDioException(DioException error) {
    final data = error.response?.data;
    if (data is Map<String, dynamic> && data['title'] is String) {
      return ApiException(data['title'] as String, statusCode: error.response?.statusCode);
    }

    if (error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout) {
      return const ApiException('Could not reach the server. Check your connection.');
    }

    return ApiException(
      error.message ?? 'Something went wrong. Try again.',
      statusCode: error.response?.statusCode,
    );
  }

  @override
  String toString() => message;
}
