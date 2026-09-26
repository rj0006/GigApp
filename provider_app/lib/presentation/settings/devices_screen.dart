import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../domain/entities/device_session.dart';
import '../common/widgets/confirm_dialog.dart';
import '../providers.dart';

class DevicesScreen extends ConsumerWidget {
  const DevicesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final devices = ref.watch(devicesProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Manage devices'),
        actions: [
          TextButton(
            onPressed: () async {
              final confirmed = await showConfirmDialog(
                context,
                title: 'Sign out other devices?',
                message: 'Every other device signed in to your account will be signed out. This device stays signed in.',
                confirmLabel: 'Yes, sign them out',
                destructive: true,
              );
              if (!confirmed) return;
              try {
                await ref.read(deviceRepositoryProvider).revokeOthers();
                ref.invalidate(devicesProvider);
              } on ApiException catch (e) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context)
                    ..hideCurrentSnackBar()
                    ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
                }
              }
            },
            child: const Text('Sign out others'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(devicesProvider),
        child: devices.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ListView(
            children: [
              const SizedBox(height: 80),
              Center(child: Text('Could not load your devices.\n$error', textAlign: TextAlign.center)),
            ],
          ),
          data: (list) => ListView(
            padding: const EdgeInsets.all(16),
            children: list.map((d) => _DeviceTile(device: d)).toList(),
          ),
        ),
      ),
    );
  }
}

class _DeviceTile extends ConsumerWidget {
  const _DeviceTile({required this.device});

  final DeviceSession device;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: Icon(
          device.isCurrent ? Icons.smartphone : Icons.devices_other,
          color: device.isCurrent ? const Color(0xFF4F46E5) : Colors.grey,
        ),
        title: Row(
          children: [
            Flexible(child: Text(device.deviceLabel, overflow: TextOverflow.ellipsis)),
            if (device.isCurrent) ...[
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                decoration: BoxDecoration(
                  color: const Color(0xFF4F46E5).withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: const Text('This device', style: TextStyle(fontSize: 11, color: Color(0xFF4F46E5))),
              ),
            ],
          ],
        ),
        subtitle: Text(
          [
            if (device.ipAddress != null) device.ipAddress,
            device.lastUsedAt != null ? 'Last used ${_formatDate(device.lastUsedAt!)}' : 'Signed in ${_formatDate(device.createdAt)}',
          ].whereType<String>().join(' · '),
          style: TextStyle(fontSize: 12, color: Colors.grey.shade600),
        ),
        trailing: device.isCurrent
            ? null
            : IconButton(
                icon: const Icon(Icons.close, color: Colors.red),
                tooltip: 'Sign out this device',
                onPressed: () async {
                  final confirmed = await showConfirmDialog(
                    context,
                    title: 'Sign out this device?',
                    destructive: true,
                  );
                  if (!confirmed) return;
                  try {
                    await ref.read(deviceRepositoryProvider).revoke(device.id);
                    ref.invalidate(devicesProvider);
                  } on ApiException catch (e) {
                    if (context.mounted) {
                      ScaffoldMessenger.of(context)
                        ..hideCurrentSnackBar()
                        ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
                    }
                  }
                },
              ),
      ),
    );
  }

  String _formatDate(DateTime dt) => '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')}/${dt.year}';
}
