import '../entities/category_detail.dart';
import '../entities/service_item.dart';
import '../entities/storefront_home.dart';

abstract class StorefrontRepository {
  Future<StorefrontHome> getHome();
  Future<CategoryDetail> getCategory(int categoryId);
  Future<List<ServiceItem>> search(String term);
  Future<void> placeOrder({required int addressId, String? note});
}
