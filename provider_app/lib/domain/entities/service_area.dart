class ServiceArea {
  const ServiceArea({
    this.baseLatitude,
    this.baseLongitude,
    required this.serviceRadiusKm,
    this.baseCity,
    this.basePincode,
    required this.hasPin,
    required this.openTasksInRange,
  });

  final double? baseLatitude;
  final double? baseLongitude;
  final int serviceRadiusKm;
  final String? baseCity;
  final String? basePincode;
  final bool hasPin;
  final int openTasksInRange;
}
