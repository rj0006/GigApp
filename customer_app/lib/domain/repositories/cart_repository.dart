import '../entities/cart.dart';

abstract class CartRepository {
  Future<Cart> getCart();
  Future<Cart> addItem(int serviceItemId, int quantity);
  Future<Cart> setQuantity(int serviceItemId, int quantity);
  Future<Cart> removeItem(int serviceItemId);
}
