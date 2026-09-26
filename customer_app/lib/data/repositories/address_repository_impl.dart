import '../../domain/entities/address.dart';
import '../../domain/repositories/address_repository.dart';
import '../datasources/address_remote_data_source.dart';

class AddressRepositoryImpl implements AddressRepository {
  AddressRepositoryImpl(this._remote);

  final AddressRemoteDataSource _remote;

  @override
  Future<List<Address>> getAddresses() => _remote.getAddresses();

  @override
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
  }) {
    return _remote.save(
      id: id,
      body: {
        'label': label,
        if (houseNumber != null && houseNumber.isNotEmpty) 'houseNumber': houseNumber,
        'line1': line1,
        if (line2 != null && line2.isNotEmpty) 'line2': line2,
        if (landmark != null && landmark.isNotEmpty) 'landmark': landmark,
        'city': city,
        if (state != null && state.isNotEmpty) 'state': state,
        'pincode': pincode,
        'latitude': latitude,
        'longitude': longitude,
        'isDefault': isDefault,
      },
    );
  }

  @override
  Future<Address> setDefault(int id) => _remote.setDefault(id);

  @override
  Future<void> delete(int id) => _remote.delete(id);
}
