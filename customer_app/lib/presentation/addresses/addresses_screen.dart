import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../domain/entities/address.dart';
import '../common/widgets/confirm_dialog.dart';
import '../providers.dart';
import 'widgets/address_form_dialog.dart';

class AddressesScreen extends ConsumerWidget {
  const AddressesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final addresses = ref.watch(addressesProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('My addresses'),
        actions: [
          IconButton(
            icon: const Icon(Icons.add),
            tooltip: 'Add address',
            onPressed: () => showAddressFormDialog(context),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(addressesProvider),
        child: addresses.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ListView(
            children: [
              const SizedBox(height: 80),
              Center(child: Text('Could not load your addresses.\n$error', textAlign: TextAlign.center)),
            ],
          ),
          data: (list) => list.isEmpty
              ? ListView(
                  children: [
                    const SizedBox(height: 80),
                    Center(
                      child: Text('No addresses saved yet.', style: TextStyle(color: Colors.grey.shade600)),
                    ),
                  ],
                )
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: list.map((a) => _AddressTile(address: a)).toList(),
                ),
        ),
      ),
    );
  }
}

class _AddressTile extends ConsumerWidget {
  const _AddressTile({required this.address});

  final Address address;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: Icon(
          address.label == 'Home'
              ? Icons.home_outlined
              : address.label == 'Office'
                  ? Icons.business_outlined
                  : Icons.place_outlined,
          color: address.isDefault ? const Color(0xFF4F46E5) : Colors.grey,
        ),
        title: Row(
          children: [
            Text(address.label, style: const TextStyle(fontWeight: FontWeight.w600)),
            if (address.isDefault) ...[
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                decoration: BoxDecoration(
                  color: const Color(0xFF4F46E5).withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: const Text('Default', style: TextStyle(fontSize: 11, color: Color(0xFF4F46E5))),
              ),
            ],
          ],
        ),
        subtitle: Text(address.singleLine),
        trailing: PopupMenuButton<String>(
          onSelected: (value) async {
            if (value == 'edit') {
              showAddressFormDialog(context, current: address);
            } else if (value == 'default') {
              try {
                await ref.read(addressRepositoryProvider).setDefault(address.id);
                ref.invalidate(addressesProvider);
              } on ApiException catch (e) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context)
                    ..hideCurrentSnackBar()
                    ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
                }
              }
            } else if (value == 'delete') {
              final confirmed = await showConfirmDialog(context, title: 'Delete this address?', destructive: true);
              if (!confirmed) return;
              try {
                await ref.read(addressRepositoryProvider).delete(address.id);
                ref.invalidate(addressesProvider);
              } on ApiException catch (e) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context)
                    ..hideCurrentSnackBar()
                    ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
                }
              }
            }
          },
          itemBuilder: (context) => [
            const PopupMenuItem(value: 'edit', child: Text('Edit')),
            if (!address.isDefault) const PopupMenuItem(value: 'default', child: Text('Set as default')),
            const PopupMenuItem(value: 'delete', child: Text('Delete')),
          ],
        ),
      ),
    );
  }
}
