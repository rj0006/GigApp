import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../../domain/entities/service_zone.dart';

class ZoneRemoteDataSource {
  ZoneRemoteDataSource(this._dio);

  final Dio _dio;

  Future<List<ServiceZoneOption>> getZones() async {
    try {
      final response = await _dio.get<List<dynamic>>(ApiEndpoints.serviceZones);
      return (response.data ?? const [])
          .map((e) => e as Map<String, dynamic>)
          .map((json) => ServiceZoneOption(id: json['id'] as int, name: json['name'] as String? ?? ''))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<NearestZone> getNearest(double lat, double lng) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.nearestServiceZone,
        queryParameters: {'lat': lat, 'lng': lng},
      );
      final json = response.data!;
      return NearestZone(
        id: json['id'] as int,
        name: json['name'] as String? ?? '',
        distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
      );
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
