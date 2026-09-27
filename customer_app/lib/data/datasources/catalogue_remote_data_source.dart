import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/service_item_model.dart';
import '../models/skill_category_model.dart';

class CatalogueRemoteDataSource {
  CatalogueRemoteDataSource(this._dio);

  final Dio _dio;

  Future<List<SkillCategoryModel>> getCategories() async {
    try {
      final response = await _dio.get<List<dynamic>>(ApiEndpoints.skillCategories);
      return (response.data ?? const [])
          .map((e) => SkillCategoryModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<List<ServiceItemModel>> getBookableItems(int categoryId, {int? zoneId}) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        ApiEndpoints.bookableServiceItems,
        queryParameters: {'categoryId': categoryId, 'zoneId': ?zoneId},
      );
      return (response.data ?? const [])
          .map((e) => ServiceItemModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
