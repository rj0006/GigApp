import 'dart:io';

import '../entities/provider_dashboard.dart';
import '../entities/service_area.dart';

abstract class PartnerRepository {
  Future<ProviderDashboard> getMyDashboard();

  Future<void> setAvailability(bool isAvailable);

  Future<void> submitKyc({
    File? selfie,
    File? aadhaarFront,
    File? aadhaarBack,
    String? aadhaarNumber,
  });

  Future<void> respondToOffer({required int offerId, required bool accepted});

  Future<ServiceArea> getServiceArea();

  Future<ServiceArea> updateServiceArea({
    double? baseLatitude,
    double? baseLongitude,
    required int serviceRadiusKm,
    String? baseCity,
    String? basePincode,
  });
}
