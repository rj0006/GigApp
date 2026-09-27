import '../entities/service_zone.dart';

abstract class ZoneRepository {
  Future<List<ServiceZoneOption>> getZones();
  Future<NearestZone> getNearest(double lat, double lng);
}
