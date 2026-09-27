import '../../domain/entities/service_zone.dart';
import '../../domain/repositories/zone_repository.dart';
import '../datasources/zone_remote_data_source.dart';

class ZoneRepositoryImpl implements ZoneRepository {
  ZoneRepositoryImpl(this._remote);

  final ZoneRemoteDataSource _remote;

  @override
  Future<List<ServiceZoneOption>> getZones() => _remote.getZones();

  @override
  Future<NearestZone> getNearest(double lat, double lng) => _remote.getNearest(lat, lng);
}
