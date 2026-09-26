import '../../domain/entities/service_area.dart';

class ServiceAreaModel extends ServiceArea {
  const ServiceAreaModel({
    super.baseLatitude,
    super.baseLongitude,
    required super.serviceRadiusKm,
    super.baseCity,
    super.basePincode,
    required super.hasPin,
    required super.openTasksInRange,
  });

  factory ServiceAreaModel.fromJson(Map<String, dynamic> json) {
    final form = json['form'] as Map<String, dynamic>? ?? const {};
    return ServiceAreaModel(
      baseLatitude: (form['baseLatitude'] as num?)?.toDouble(),
      baseLongitude: (form['baseLongitude'] as num?)?.toDouble(),
      serviceRadiusKm: form['serviceRadiusKm'] as int? ?? 10,
      baseCity: form['baseCity'] as String?,
      basePincode: form['basePincode'] as String?,
      hasPin: json['hasPin'] as bool? ?? false,
      openTasksInRange: json['openTasksInRange'] as int? ?? 0,
    );
  }
}
