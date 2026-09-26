import 'package:dio/dio.dart';

import '../../domain/entities/auth_tokens.dart';
import '../config/env.dart';
import '../storage/token_storage.dart';
import 'api_endpoints.dart';

class AuthInterceptor extends Interceptor {
  AuthInterceptor(this._tokenStorage, {this.onSessionExpired});

  final TokenStorage _tokenStorage;
  final Future<void> Function()? onSessionExpired;

  Future<String?>? _refreshInFlight;

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    try {
      final tokens = await _tokenStorage.read();
      if (tokens != null) {
        options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
      }
    } catch (_) {
      // A corrupt or unreadable stored token should not block the request —
      // the server will reply 401 and the normal refresh/logout path takes over.
    }
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final isUnauthorized = err.response?.statusCode == 401;
    final isRefreshCall = err.requestOptions.path.contains(ApiEndpoints.refresh);

    if (!isUnauthorized || isRefreshCall) {
      handler.next(err);
      return;
    }

    final newAccessToken = await _refreshSession();

    if (newAccessToken == null) {
      await _tokenStorage.clear();
      if (onSessionExpired != null) await onSessionExpired!();
      handler.next(err);
      return;
    }

    final retryDio = Dio(BaseOptions(baseUrl: Env.apiBaseUrl));
    final retriedOptions = err.requestOptions
      ..headers['Authorization'] = 'Bearer $newAccessToken';

    try {
      final response = await retryDio.fetch(retriedOptions);
      handler.resolve(response);
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }

  Future<String?> _refreshSession() {
    return _refreshInFlight ??= _performRefresh().whenComplete(() {
      _refreshInFlight = null;
    });
  }

  Future<String?> _performRefresh() async {
    final tokens = await _tokenStorage.read();
    if (tokens == null) return null;

    final plainDio = Dio(BaseOptions(baseUrl: Env.apiBaseUrl));

    try {
      final response = await plainDio.post<Map<String, dynamic>>(
        ApiEndpoints.refresh,
        data: {'refreshToken': tokens.refreshToken},
      );

      final body = response.data!;
      final refreshed = AuthTokens(
        accessToken: body['token'] as String,
        expiresAtUtc: DateTime.parse(body['expiresAtUtc'] as String),
        refreshToken: body['refreshToken'] as String,
        refreshExpiresAtUtc: DateTime.parse(body['refreshExpiresAtUtc'] as String),
      );

      await _tokenStorage.save(refreshed);
      return refreshed.accessToken;
    } on DioException {
      return null;
    }
  }
}
