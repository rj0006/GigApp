import '../entities/category_detail.dart';
import '../entities/service_item.dart';
import '../entities/storefront_home.dart';

abstract class StorefrontRepository {
  Future<StorefrontHome> getHome(int zoneId);
  Future<CategoryDetail> getCategory(int categoryId, int zoneId);
  Future<List<ServiceItem>> search(String term, int zoneId);
  Future<void> placeOrder({required int addressId, String? note});
}
