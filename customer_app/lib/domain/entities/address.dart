class Address {
  const Address({
    required this.id,
    required this.label,
    required this.line1,
    required this.city,
    required this.pincode,
    required this.isDefault,
    required this.singleLine,
    this.houseNumber,
    this.line2,
    this.landmark,
    this.state,
    this.latitude,
    this.longitude,
  });

  final int id;
  final String label;
  final String? houseNumber;
  final String line1;
  final String? line2;
  final String? landmark;
  final String city;
  final String? state;
  final String pincode;
  final double? latitude;
  final double? longitude;
  final bool isDefault;
  final String singleLine;
}
