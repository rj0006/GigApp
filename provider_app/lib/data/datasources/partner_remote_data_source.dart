import 'dart:io';

import 'package:dio/dio.dart';

import '../../core/network/api_endpoints.dart';
import '../../core/network/api_exception.dart';
import '../models/provider_dashboard_model.dart';
import '../models/service_area_model.dart';

class PartnerRemoteDataSource {
  PartnerRemoteDataSource(this._dio);

  final Dio _dio;

  Future<ProviderDashboardModel> getMyDashboard() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.providerDashboard);
      return ProviderDashboardModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> setAvailability(bool isAvailable) async {
    try {
      await _dio.put<void>(ApiEndpoints.availability, data: {'isAvailable': isAvailable});
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> submitKyc({
    File? selfie,
    File? aadhaarFront,
    File? aadhaarBack,
    String? aadhaarNumber,
  }) async {
    try {
      final formData = FormData.fromMap({
        if (selfie != null) 'Selfie': await MultipartFile.fromFile(selfie.path),
        if (aadhaarFront != null) 'AadhaarFront': await MultipartFile.fromFile(aadhaarFront.path),
        if (aadhaarBack != null) 'AadhaarBack': await MultipartFile.fromFile(aadhaarBack.path),
        if (aadhaarNumber != null && aadhaarNumber.isNotEmpty) 'AadhaarNumber': aadhaarNumber,
      });
      await _dio.post<void>(ApiEndpoints.submitKyc, data: formData);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<void> respondToOffer({required int offerId, required bool accepted}) async {
    try {
      await _dio.post<void>(ApiEndpoints.offerRespond(offerId), data: {'accepted': accepted});
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<ServiceAreaModel> getServiceArea() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(ApiEndpoints.serviceArea);
      return ServiceAreaModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  Future<ServiceAreaModel> updateServiceArea(Map<String, dynamic> body) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(ApiEndpoints.serviceArea, data: body);
      return ServiceAreaModel.fromJson(response.data!);
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }
}
