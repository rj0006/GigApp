import '../../domain/entities/category_detail.dart';
import '../../domain/entities/service_item.dart';
import '../../domain/entities/storefront_home.dart';
import '../../domain/repositories/storefront_repository.dart';
import '../datasources/storefront_remote_data_source.dart';
import '../models/service_item_model.dart';

class StorefrontRepositoryImpl implements StorefrontRepository {
  StorefrontRepositoryImpl(this._remote);

  final StorefrontRemoteDataSource _remote;

  @override
  Future<StorefrontHome> getHome(int zoneId) => _remote.getHome(zoneId);

  @override
  Future<CategoryDetail> getCategory(int categoryId, int zoneId) async {
    final json = await _remote.getCategory(categoryId, zoneId);
    final category = json['category'] as Map<String, dynamic>? ?? const {};
    final services = json['services'] as List<dynamic>? ?? const [];
    return CategoryDetail(
      categoryName: category['name'] as String? ?? '',
      services: services.map((e) => ServiceItemModel.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }

  @override
  Future<List<ServiceItem>> search(String term, int zoneId) => _remote.search(term, zoneId);

  @override
  Future<void> placeOrder({required int addressId, String? note}) =>
      _remote.placeOrder(addressId: addressId, note: note);
}
