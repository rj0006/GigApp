import '../../domain/entities/address.dart';

class AddressModel extends Address {
  const AddressModel({
    required super.id,
    required super.label,
    required super.line1,
    required super.city,
    required super.pincode,
    required super.isDefault,
    required super.singleLine,
    super.houseNumber,
    super.line2,
    super.landmark,
    super.state,
    super.latitude,
    super.longitude,
  });

  factory AddressModel.fromJson(Map<String, dynamic> json) {
    return AddressModel(
      id: json['id'] as int,
      label: json['label'] as String? ?? 'Home',
      houseNumber: json['houseNumber'] as String?,
      line1: json['line1'] as String? ?? '',
      line2: json['line2'] as String?,
      landmark: json['landmark'] as String?,
      city: json['city'] as String? ?? '',
      state: json['state'] as String?,
      pincode: json['pincode'] as String? ?? '',
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      isDefault: json['isDefault'] as bool? ?? false,
      singleLine: json['singleLine'] as String? ?? '',
    );
  }
}
