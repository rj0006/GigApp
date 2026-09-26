import '../../domain/entities/cart.dart';
import '../../domain/repositories/cart_repository.dart';
import '../datasources/cart_remote_data_source.dart';

class CartRepositoryImpl implements CartRepository {
  CartRepositoryImpl(this._remote);

  final CartRemoteDataSource _remote;

  @override
  Future<Cart> getCart() => _remote.getCart();

  @override
  Future<Cart> addItem(int serviceItemId, int quantity) => _remote.addItem(serviceItemId, quantity);

  @override
  Future<Cart> setQuantity(int serviceItemId, int quantity) => _remote.setQuantity(serviceItemId, quantity);

  @override
  Future<Cart> removeItem(int serviceItemId) => _remote.removeItem(serviceItemId);
}
