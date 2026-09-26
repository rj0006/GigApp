import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../providers.dart';

class AddToCartControl extends ConsumerStatefulWidget {
  const AddToCartControl({super.key, required this.serviceItemId});

  final int serviceItemId;

  @override
  ConsumerState<AddToCartControl> createState() => _AddToCartControlState();
}

class _AddToCartControlState extends ConsumerState<AddToCartControl> {
  bool _busy = false;

  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await action();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final cart = ref.watch(cartControllerProvider);
    final quantity = cart.valueOrNull?.quantityOf(widget.serviceItemId) ?? 0;
    final controller = ref.read(cartControllerProvider.notifier);

    if (quantity == 0) {
      return SizedBox(
        height: 32,
        child: OutlinedButton(
          onPressed: _busy ? null : () => _run(() => controller.add(widget.serviceItemId)),
          style: OutlinedButton.styleFrom(
            padding: const EdgeInsets.symmetric(horizontal: 14),
            foregroundColor: const Color(0xFF4F46E5),
            side: const BorderSide(color: Color(0xFF4F46E5)),
          ),
          child: _busy
              ? const SizedBox(height: 14, width: 14, child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Add'),
        ),
      );
    }

    return Container(
      height: 32,
      decoration: BoxDecoration(
        color: const Color(0xFF4F46E5),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _StepButton(
            icon: Icons.remove,
            onTap: _busy ? null : () => _run(() => controller.setQuantity(widget.serviceItemId, quantity - 1)),
          ),
          SizedBox(
            width: 22,
            child: Text('$quantity', textAlign: TextAlign.center, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700)),
          ),
          _StepButton(
            icon: Icons.add,
            onTap: _busy ? null : () => _run(() => controller.setQuantity(widget.serviceItemId, quantity + 1)),
          ),
        ],
      ),
    );
  }
}

class _StepButton extends StatelessWidget {
  const _StepButton({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: SizedBox(width: 28, height: 32, child: Icon(icon, color: Colors.white, size: 16)),
    );
  }
}
