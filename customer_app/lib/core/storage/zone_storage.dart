import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class ZoneStorage {
  ZoneStorage(this._storage);

  final FlutterSecureStorage _storage;

  static const _zoneIdKey = 'gigapp.zoneId';
  static const _zoneNameKey = 'gigapp.zoneName';

  Future<void> save(int zoneId, String zoneName) async {
    await _storage.write(key: _zoneIdKey, value: '$zoneId');
    await _storage.write(key: _zoneNameKey, value: zoneName);
  }

  Future<(int, String)?> read() async {
    final id = await _storage.read(key: _zoneIdKey);
    final name = await _storage.read(key: _zoneNameKey);
    if (id == null || name == null) return null;

    final parsed = int.tryParse(id);
    return parsed == null ? null : (parsed, name);
  }

  Future<void> clear() async {
    await _storage.delete(key: _zoneIdKey);
    await _storage.delete(key: _zoneNameKey);
  }
}
