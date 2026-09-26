import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../../core/storage/token_storage.dart';
import '../models/device_session_model.dart';

class DeviceRemoteDataSource {
  DeviceRemoteDataSource(this._dio, this._tokenStorage);

  final Dio _dio;
  final TokenStorage _tokenStorage;

  // The web portals identify "this device" via the gigapp_refresh cookie;
  // mobile has no cookie jar, so it sends the raw refresh token it already
  // holds through this header instead — see DevicesController.cs.
  Future<Options> _currentDeviceHeader() async {
    final tokens = await _tokenStorage.read();
    return Options(headers: {'X-Refresh-Token': tokens?.refreshToken});
  }

  Future<List<DeviceSessionModel>> getDevices() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        ApiEndpoints.devices,
        options: await _currentDeviceHeader(),
      );
      return (response.data ?? const [])
          .map((e) => DeviceSessionModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> revoke(int id) async {
    try {
      await _dio.delete<void>(ApiEndpoints.deviceRevoke(id));
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> revokeOthers() async {
    try {
      await _dio.post<void>(
        '${ApiEndpoints.devices}/revoke-others',
        options: await _currentDeviceHeader(),
      );
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
