import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/address_model.dart';

class AddressRemoteDataSource {
  AddressRemoteDataSource(this._dio);

  final Dio _dio;

  Future<List<AddressModel>> getAddresses() async {
    try {
      final response = await _dio.get<List<dynamic>>(ApiEndpoints.addresses);
      return (response.data ?? const [])
          .map((e) => AddressModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<AddressModel> save({int? id, required Map<String, dynamic> body}) async {
    try {
      final response = id == null
          ? await _dio.post<Map<String, dynamic>>(ApiEndpoints.addresses, data: body)
          : await _dio.put<Map<String, dynamic>>(ApiEndpoints.addressItem(id), data: body);
      return AddressModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<AddressModel> setDefault(int id) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(ApiEndpoints.addressDefault(id));
      return AddressModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> delete(int id) async {
    try {
      await _dio.delete<void>(ApiEndpoints.addressItem(id));
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
