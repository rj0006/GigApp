import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/open_login.dart';
import '../../auth/session_controller.dart';
import '../../providers.dart';
import '../cart_screen.dart';

class CartButton extends ConsumerWidget {
  const CartButton({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isGuest = ref.watch(sessionControllerProvider).valueOrNull == null;
    final cart = ref.watch(cartControllerProvider);
    final itemCount = isGuest ? 0 : cart.valueOrNull?.itemCount ?? 0;

    return IconButton(
      tooltip: 'Cart',
      onPressed: () => isGuest
          ? openLogin(context)
          : Navigator.of(context).push(MaterialPageRoute(builder: (_) => const CartScreen())),
      icon: Badge(
        label: Text('$itemCount'),
        isLabelVisible: itemCount > 0,
        child: const Icon(Icons.shopping_cart_outlined),
      ),
    );
  }
}
