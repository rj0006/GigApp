import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/app_notification.dart';
import '../providers.dart';

class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(notificationsControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          TextButton(
            onPressed: () async {
              await ref.read(notificationRepositoryProvider).markRead();
              ref.invalidate(notificationSummaryProvider);
              await ref
                  .read(notificationsControllerProvider.notifier)
                  .refresh();
            },
            child: const Text('Mark all read'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(notificationSummaryProvider);
          await ref.read(notificationsControllerProvider.notifier).refresh();
        },
        child: state.items.isEmpty && state.isLoading
            ? const Center(child: CircularProgressIndicator())
            : state.items.isEmpty && state.error != null
            ? ListView(
                children: [
                  const SizedBox(height: 100),
                  Center(
                    child: Text(
                      'Could not load notifications.\n${state.error}',
                      textAlign: TextAlign.center,
                    ),
                  ),
                ],
              )
            : state.items.isEmpty
            ? ListView(
                children: [
                  const SizedBox(height: 100),
                  Center(
                    child: Text(
                      'Nothing yet. We will tell you when a job moves.',
                      style: TextStyle(color: Colors.grey.shade600),
                    ),
                  ),
                ],
              )
            : ListView(
                children: [
                  ...state.items.map((n) => _NotificationTile(notification: n)),
                  if (state.hasNext)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Center(
                        child: state.isLoading
                            ? const CircularProgressIndicator()
                            : TextButton(
                                onPressed: () => ref
                                    .read(
                                      notificationsControllerProvider.notifier,
                                    )
                                    .loadMore(),
                                child: const Text('Load more'),
                              ),
                      ),
                    ),
                ],
              ),
      ),
    );
  }
}

class _NotificationTile extends ConsumerWidget {
  const _NotificationTile({required this.notification});

  final AppNotification notification;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ListTile(
      tileColor: notification.isRead
          ? null
          : const Color(0xFF4F46E5).withValues(alpha: 0.05),
      leading: CircleAvatar(
        backgroundColor: const Color(0xFF4F46E5).withValues(alpha: 0.12),
        child: Text(notification.icon),
      ),
      title: Text(
        notification.title,
        style: const TextStyle(fontWeight: FontWeight.w600),
      ),
      subtitle: Text(notification.body),
      trailing: Text(
        notification.age,
        style: TextStyle(fontSize: 11, color: Colors.grey.shade500),
      ),
      onTap: notification.isRead
          ? null
          : () async {
              await ref
                  .read(notificationRepositoryProvider)
                  .markRead(notificationId: notification.id);
              ref.invalidate(notificationSummaryProvider);
              await ref
                  .read(notificationsControllerProvider.notifier)
                  .refresh();
            },
    );
  }
}
