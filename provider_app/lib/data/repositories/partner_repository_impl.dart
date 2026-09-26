import 'dart:io';

import '../../domain/entities/provider_dashboard.dart';
import '../../domain/entities/service_area.dart';
import '../../domain/repositories/partner_repository.dart';
import '../datasources/partner_remote_data_source.dart';

class PartnerRepositoryImpl implements PartnerRepository {
  PartnerRepositoryImpl(this._remote);

  final PartnerRemoteDataSource _remote;

  @override
  Future<ProviderDashboard> getMyDashboard() => _remote.getMyDashboard();

  @override
  Future<void> setAvailability(bool isAvailable) => _remote.setAvailability(isAvailable);

  @override
  Future<void> submitKyc({File? selfie, File? aadhaarFront, File? aadhaarBack, String? aadhaarNumber}) {
    return _remote.submitKyc(
      selfie: selfie,
      aadhaarFront: aadhaarFront,
      aadhaarBack: aadhaarBack,
      aadhaarNumber: aadhaarNumber,
    );
  }

  @override
  Future<void> respondToOffer({required int offerId, required bool accepted}) {
    return _remote.respondToOffer(offerId: offerId, accepted: accepted);
  }

  @override
  Future<ServiceArea> getServiceArea() => _remote.getServiceArea();

  @override
  Future<ServiceArea> updateServiceArea({
    double? baseLatitude,
    double? baseLongitude,
    required int serviceRadiusKm,
    String? baseCity,
    String? basePincode,
  }) {
    return _remote.updateServiceArea({
      'baseLatitude': baseLatitude,
      'baseLongitude': baseLongitude,
      'serviceRadiusKm': serviceRadiusKm,
      if (baseCity != null && baseCity.isNotEmpty) 'baseCity': baseCity,
      if (basePincode != null && basePincode.isNotEmpty) 'basePincode': basePincode,
    });
  }
}
