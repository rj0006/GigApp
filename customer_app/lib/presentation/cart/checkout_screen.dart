import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../domain/entities/address.dart';
import '../addresses/widgets/address_form_dialog.dart';
import '../providers.dart';

class CheckoutScreen extends ConsumerStatefulWidget {
  const CheckoutScreen({super.key});

  @override
  ConsumerState<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends ConsumerState<CheckoutScreen> {
  Address? _address;
  final _noteController = TextEditingController();
  bool _isSubmitting = false;
  String? _error;

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final cart = ref.watch(cartControllerProvider);
    final addresses = ref.watch(addressesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Checkout')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_error != null) ...[
            Text(_error!, style: const TextStyle(color: Colors.red)),
            const SizedBox(height: 12),
          ],
          Card(
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Row(
                children: [
                  const Icon(Icons.shopping_bag_outlined, color: Color(0xFF4F46E5)),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      '${cart.valueOrNull?.itemCount ?? 0} item(s) · ₹${(cart.valueOrNull?.total ?? 0).round()}',
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),
          Text('Deliver to', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          addresses.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => Text('Could not load addresses.\n$error'),
            data: (list) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ...list.map((a) => RadioListTile<Address>(
                      value: a,
                      groupValue: _address,
                      onChanged: (value) => setState(() => _address = value),
                      title: Text(a.label),
                      subtitle: Text(a.singleLine),
                      contentPadding: EdgeInsets.zero,
                    )),
                TextButton.icon(
                  onPressed: () async {
                    final saved = await showAddressFormDialog(context);
                    if (saved != null) setState(() => _address = saved);
                  },
                  icon: const Icon(Icons.add),
                  label: const Text('Add a new address'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Text('Note for the partner (optional)', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          TextField(
            controller: _noteController,
            maxLines: 2,
            decoration: const InputDecoration(
              hintText: 'Anything the partner should know before they arrive',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 24),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: _isSubmitting ? null : _submit,
              child: _isSubmitting
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Place order'),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    if (_address == null) {
      setState(() => _error = 'Choose where the work is needed.');
      return;
    }

    setState(() {
      _isSubmitting = true;
      _error = null;
    });

    try {
      await ref.read(storefrontRepositoryProvider).placeOrder(
            addressId: _address!.id,
            note: _noteController.text.trim().isEmpty ? null : _noteController.text.trim(),
          );
      await ref.read(cartControllerProvider.notifier).refresh();
      ref.invalidate(myTasksProvider);
      ref.invalidate(orderHistoryControllerProvider);
      if (mounted) {
        Navigator.of(context).popUntil((route) => route.isFirst);
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(const SnackBar(
            content: Text('Booked. We are finding you the nearest partner now.'),
            backgroundColor: Colors.green,
          ));
      }
    } on ApiException catch (e) {
      setState(() {
        _isSubmitting = false;
        _error = e.message;
      });
    }
  }
}
