import 'package:dio/dio.dart';

import '../../core/network/api_exception.dart';
import '../models/skill_category_model.dart';

class SkillCategoryRemoteDataSource {
  SkillCategoryRemoteDataSource(this._dio);

  final Dio _dio;

  Future<List<SkillCategoryModel>> getActive() async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/skillcategories');
      return response.data!
          .map((json) => SkillCategoryModel.fromJson(json as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
