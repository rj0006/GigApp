import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../domain/entities/auth_tokens.dart';

class TokenStorage {
  TokenStorage(this._storage);

  final FlutterSecureStorage _storage;

  static const _accessTokenKey = 'gigapp.accessToken';
  static const _accessExpiryKey = 'gigapp.accessExpiresAtUtc';
  static const _refreshTokenKey = 'gigapp.refreshToken';
  static const _refreshExpiryKey = 'gigapp.refreshExpiresAtUtc';

  Future<void> save(AuthTokens tokens) async {
    // Writes must be sequential, not Future.wait-ed in parallel: the web
    // backend's own wrapping key is created lazily on first write via a
    // check-then-create on localStorage with no locking, so concurrent
    // writes can each generate a different key and overwrite each other,
    // leaving earlier values permanently encrypted under a key that is no
    // longer there. One write at a time keeps every value under one key.
    await _storage.write(key: _accessTokenKey, value: tokens.accessToken);
    await _storage.write(key: _accessExpiryKey, value: tokens.expiresAtUtc.toIso8601String());
    await _storage.write(key: _refreshTokenKey, value: tokens.refreshToken);
    await _storage.write(
      key: _refreshExpiryKey,
      value: tokens.refreshExpiresAtUtc.toIso8601String(),
    );
  }

  Future<AuthTokens?> read() async {
    final accessToken = await _storage.read(key: _accessTokenKey);
    final accessExpiry = await _storage.read(key: _accessExpiryKey);
    final refreshToken = await _storage.read(key: _refreshTokenKey);
    final refreshExpiry = await _storage.read(key: _refreshExpiryKey);

    if (accessToken == null || refreshToken == null || refreshExpiry == null) return null;

    return AuthTokens(
      accessToken: accessToken,
      expiresAtUtc: DateTime.tryParse(accessExpiry ?? '') ?? DateTime.now().toUtc(),
      refreshToken: refreshToken,
      refreshExpiresAtUtc: DateTime.tryParse(refreshExpiry) ?? DateTime.now().toUtc(),
    );
  }

  Future<void> clear() async {
    await Future.wait([
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _accessExpiryKey),
      _storage.delete(key: _refreshTokenKey),
      _storage.delete(key: _refreshExpiryKey),
    ]);
  }
}
