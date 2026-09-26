import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/cart.dart';
import '../../domain/repositories/cart_repository.dart';

class CartController extends StateNotifier<AsyncValue<Cart>> {
  CartController(this._repository) : super(const AsyncValue.loading()) {
    refresh();
  }

  final CartRepository _repository;

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(_repository.getCart);
  }

  Future<void> add(int serviceItemId, {int quantity = 1}) async {
    final cart = await _repository.addItem(serviceItemId, quantity);
    state = AsyncValue.data(cart);
  }

  Future<void> setQuantity(int serviceItemId, int quantity) async {
    final cart = await _repository.setQuantity(serviceItemId, quantity);
    state = AsyncValue.data(cart);
  }

  Future<void> remove(int serviceItemId) async {
    final cart = await _repository.removeItem(serviceItemId);
    state = AsyncValue.data(cart);
  }
}
