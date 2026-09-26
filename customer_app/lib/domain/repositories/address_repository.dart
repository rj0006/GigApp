import '../entities/address.dart';

abstract class AddressRepository {
  Future<List<Address>> getAddresses();

  Future<Address> save({
    int? id,
    required String label,
    String? houseNumber,
    required String line1,
    String? line2,
    String? landmark,
    required String city,
    String? state,
    required String pincode,
    double? latitude,
    double? longitude,
    bool isDefault = false,
  });

  Future<Address> setDefault(int id);

  Future<void> delete(int id);
}
