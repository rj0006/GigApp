import '../../domain/entities/device_session.dart';
import '../../domain/repositories/device_repository.dart';
import '../datasources/device_remote_data_source.dart';

class DeviceRepositoryImpl implements DeviceRepository {
  DeviceRepositoryImpl(this._remote);

  final DeviceRemoteDataSource _remote;

  @override
  Future<List<DeviceSession>> getDevices() => _remote.getDevices();

  @override
  Future<void> revoke(int id) => _remote.revoke(id);

  @override
  Future<void> revokeOthers() => _remote.revokeOthers();
}
