import '../entities/device_session.dart';

abstract class DeviceRepository {
  Future<List<DeviceSession>> getDevices();
  Future<void> revoke(int id);
  Future<void> revokeOthers();
}
