import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/service_item_model.dart';
import '../models/storefront_home_model.dart';

class StorefrontRemoteDataSource {
  StorefrontRemoteDataSource(this._dio);

  final Dio _dio;

  Future<StorefrontHomeModel> getHome() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.storefrontHome);
      return StorefrontHomeModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<Map<String, dynamic>> getCategory(int categoryId) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.storefrontCategory(categoryId));
      return response.data!;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<List<ServiceItemModel>> search(String term) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.storefrontSearch,
        queryParameters: {'q': term},
      );
      final results = response.data?['results'] as List<dynamic>? ?? const [];
      return results.map((e) => ServiceItemModel.fromJson(e as Map<String, dynamic>)).toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<Map<String, dynamic>> placeOrder({required int addressId, String? note}) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.checkout,
        data: {'addressId': addressId, 'note': note},
      );
      return response.data!;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
