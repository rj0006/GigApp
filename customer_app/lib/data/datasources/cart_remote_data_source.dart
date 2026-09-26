import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/cart_model.dart';

class CartRemoteDataSource {
  CartRemoteDataSource(this._dio);

  final Dio _dio;

  Future<CartModel> getCart() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.cart);
      return CartModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<CartModel> addItem(int serviceItemId, int quantity) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.cartItems,
        data: {'serviceItemId': serviceItemId, 'quantity': quantity},
      );
      return CartModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<CartModel> setQuantity(int serviceItemId, int quantity) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        ApiEndpoints.cartItem(serviceItemId),
        data: {'quantity': quantity},
      );
      return CartModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<CartModel> removeItem(int serviceItemId) async {
    try {
      final response = await _dio.delete<Map<String, dynamic>>(ApiEndpoints.cartItem(serviceItemId));
      return CartModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
