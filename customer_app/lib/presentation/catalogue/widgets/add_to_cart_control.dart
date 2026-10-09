import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
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
          ..showSnackBar(SnackBar(content: Text(e.message)));
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
        height: 34,
        child: TextButton(
          onPressed: _busy ? null : () => _run(() => controller.add(widget.serviceItemId)),
          style: TextButton.styleFrom(
            minimumSize: const Size(64, 34),
            padding: const EdgeInsets.symmetric(horizontal: 16),
            backgroundColor: AppColors.accentSoft,
            foregroundColor: AppColors.accent,
            shape: const StadiumBorder(),
          ),
          child: _busy
              ? const SizedBox(height: 14, width: 14, child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Add'),
        ),
      );
    }

    return Container(
      height: 34,
      decoration: BoxDecoration(
        gradient: AppColors.brandGradient,
        borderRadius: BorderRadius.circular(999),
        boxShadow: AppColors.accentGlow,
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
            child: Text(
              '$quantity',
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800),
            ),
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
      customBorder: const CircleBorder(),
      child: SizedBox(width: 30, height: 34, child: Icon(icon, color: Colors.white, size: 16)),
    );
  }
}
