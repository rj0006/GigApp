class ServiceZoneOption {
  const ServiceZoneOption({required this.id, required this.name});

  final int id;
  final String name;
}

class NearestZone {
  const NearestZone({required this.id, required this.name, required this.distanceKm});

  final int id;
  final String name;
  final double distanceKm;
}
