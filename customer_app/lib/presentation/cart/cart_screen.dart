import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/cart.dart';
import '../catalogue/widgets/add_to_cart_control.dart';
import '../providers.dart';
import 'checkout_screen.dart';

class CartScreen extends ConsumerWidget {
  const CartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cart = ref.watch(cartControllerProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Your cart')),
      body: cart.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(child: Text('Could not load your cart.\n$error', textAlign: TextAlign.center)),
        data: (value) => value.isEmpty ? const _EmptyCart() : _CartBody(cart: value),
      ),
    );
  }
}

class _EmptyCart extends StatelessWidget {
  const _EmptyCart();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.shopping_cart_outlined, size: 56, color: Colors.grey.shade400),
          const SizedBox(height: 12),
          Text('Your cart is empty', style: TextStyle(color: Colors.grey.shade600)),
        ],
      ),
    );
  }
}

class _CartBody extends StatelessWidget {
  const _CartBody({required this.cart});

  final Cart cart;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Expanded(
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: cart.lines.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (_, index) => _CartLineTile(line: cart.lines[index]),
          ),
        ),
        Container(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
          decoration: BoxDecoration(
            color: Theme.of(context).scaffoldBackgroundColor,
            boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.06), blurRadius: 8, offset: const Offset(0, -2))],
          ),
          child: SafeArea(
            top: false,
            child: Row(
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Total', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                    Text('₹${cart.total.round()}', style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 18)),
                  ],
                ),
                const Spacer(),
                FilledButton(
                  onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const CheckoutScreen())),
                  child: const Text('Checkout'),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _CartLineTile extends StatelessWidget {
  const _CartLineTile({required this.line});

  final CartLine line;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(10),
        child: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: SizedBox(
                width: 56,
                height: 56,
                child: line.imageUrl == null
                    ? Container(color: Colors.grey.shade100, child: const Icon(Icons.build_outlined, color: Colors.grey))
                    : Image.network(
                        line.imageUrl!,
                        fit: BoxFit.cover,
                        errorBuilder: (_, __, ___) =>
                            Container(color: Colors.grey.shade100, child: const Icon(Icons.build_outlined, color: Colors.grey)),
                      ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(line.serviceItemName, style: const TextStyle(fontWeight: FontWeight.w600)),
                  const SizedBox(height: 2),
                  Text('₹${line.unitPrice.round()} each', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                  const SizedBox(height: 6),
                  Text('₹${line.lineTotal.round()}', style: const TextStyle(fontWeight: FontWeight.w700)),
                ],
              ),
            ),
            AddToCartControl(serviceItemId: line.serviceItemId),
          ],
        ),
      ),
    );
  }
}
