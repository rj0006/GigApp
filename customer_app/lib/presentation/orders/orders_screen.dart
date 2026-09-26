import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../domain/entities/gig_task.dart';
import '../common/widgets/confirm_dialog.dart';
import '../providers.dart';
import 'widgets/raise_help_dialog.dart';
import 'widgets/rate_partner_dialog.dart';

const _activeStatuses = ['pending', 'accepted', 'in_progress'];

class OrdersScreen extends ConsumerStatefulWidget {
  const OrdersScreen({super.key});

  @override
  ConsumerState<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends ConsumerState<OrdersScreen> with SingleTickerProviderStateMixin {
  late final TabController _tabController = TabController(length: 3, vsync: this);

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(orderHistoryControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Orders'),
        bottom: TabBar(
          controller: _tabController,
          tabs: const [Tab(text: 'Active'), Tab(text: 'Completed'), Tab(text: 'Cancelled')],
        ),
      ),
      body: state.error != null && state.orders.isEmpty
          ? Center(child: Text('Could not load your orders.\n${state.error}', textAlign: TextAlign.center))
          : TabBarView(
              controller: _tabController,
              children: [
                _OrderList(
                  orders: state.orders.where((o) => _activeStatuses.contains(o.status)).toList(),
                  isLoading: state.isLoading,
                  hasNext: state.hasNext,
                  emptyText: 'No active orders.',
                  state: state,
                ),
                _OrderList(
                  orders: state.orders.where((o) => o.status == 'completed').toList(),
                  isLoading: state.isLoading,
                  hasNext: state.hasNext,
                  emptyText: 'Nothing completed yet.',
                  state: state,
                ),
                _OrderList(
                  orders: state.orders.where((o) => o.status == 'cancelled').toList(),
                  isLoading: state.isLoading,
                  hasNext: state.hasNext,
                  emptyText: 'Nothing cancelled.',
                  state: state,
                ),
              ],
            ),
    );
  }
}

class _OrderList extends ConsumerWidget {
  const _OrderList({
    required this.orders,
    required this.isLoading,
    required this.hasNext,
    required this.emptyText,
    required this.state,
  });

  final List<GigTask> orders;
  final bool isLoading;
  final bool hasNext;
  final String emptyText;
  final dynamic state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return RefreshIndicator(
      onRefresh: () => ref.read(orderHistoryControllerProvider.notifier).refresh(),
      child: orders.isEmpty && isLoading
          ? const Center(child: CircularProgressIndicator())
          : orders.isEmpty
              ? ListView(
                  children: [
                    const SizedBox(height: 100),
                    Center(child: Text(emptyText, style: TextStyle(color: Colors.grey.shade600))),
                  ],
                )
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    ...orders.map((o) => _OrderCard(order: o, ratedByMe: state.ratings[o.id])),
                    if (hasNext)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        child: Center(
                          child: isLoading
                              ? const CircularProgressIndicator()
                              : TextButton(
                                  onPressed: () => ref.read(orderHistoryControllerProvider.notifier).loadMore(),
                                  child: const Text('Load more'),
                                ),
                        ),
                      ),
                  ],
                ),
    );
  }
}

class _OrderCard extends ConsumerWidget {
  const _OrderCard({required this.order, this.ratedByMe});

  final GigTask order;
  final dynamic ratedByMe;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final color = switch (order.status) {
      'completed' => Colors.green,
      'cancelled' => Colors.red,
      'in_progress' => Colors.blue,
      'accepted' => const Color(0xFF4F46E5),
      _ => Colors.orange,
    };

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    order.serviceItemName ?? order.categoryName,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(20)),
                  child: Text(order.statusLabel, style: TextStyle(color: color, fontSize: 11, fontWeight: FontWeight.w600)),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(order.description, style: TextStyle(color: Colors.grey.shade700, fontSize: 13)),
            const SizedBox(height: 4),
            Text(order.address, style: TextStyle(color: Colors.grey.shade500, fontSize: 12)),
            const SizedBox(height: 6),
            Row(
              children: [
                Text('₹${order.effectiveAmount.round()}', style: const TextStyle(fontWeight: FontWeight.w700)),
                if (order.partnerName != null) ...[
                  const SizedBox(width: 10),
                  Text('· ${order.partnerName}', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                ],
              ],
            ),
            if (_activeStatuses.contains(order.status) || order.status == 'completed') ...[
              const SizedBox(height: 10),
              Row(
                children: [
                  if (_activeStatuses.contains(order.status))
                    TextButton(
                      onPressed: () => _cancel(context, ref),
                      child: const Text('Cancel', style: TextStyle(color: Colors.red)),
                    ),
                  if (order.status == 'completed' && order.partnerName != null && ratedByMe == null)
                    TextButton(
                      onPressed: () => _rate(context, ref),
                      child: const Text('Rate partner'),
                    ),
                  if (ratedByMe != null)
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 8),
                      child: Row(
                        children: [
                          const Icon(Icons.star, color: Colors.amber, size: 16),
                          Text(' ${ratedByMe.stars}/5 rated', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                        ],
                      ),
                    ),
                  const Spacer(),
                  TextButton(
                    onPressed: () => _getHelp(context, ref),
                    child: const Text('Get help'),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _cancel(BuildContext context, WidgetRef ref) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Cancel this order?',
      message: 'The partner will be told this order was cancelled. This cannot be undone.',
      destructive: true,
    );
    if (!confirmed) return;

    try {
      final updated = await ref.read(taskRepositoryProvider).cancelTask(order.id);
      ref.read(orderHistoryControllerProvider.notifier).replaceTask(updated);
      ref.invalidate(myTasksProvider);
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
      }
    }
  }

  Future<void> _rate(BuildContext context, WidgetRef ref) async {
    final result = await showRatePartnerDialog(context, partnerName: order.partnerName!);
    if (result == null) return;

    try {
      await ref.read(taskRepositoryProvider).rateTask(order.id, stars: result.stars, feedback: result.feedback);
      ref.read(orderHistoryControllerProvider.notifier).refresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(const SnackBar(content: Text('Thanks for rating.'), backgroundColor: Colors.green));
      }
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
      }
    }
  }

  Future<void> _getHelp(BuildContext context, WidgetRef ref) async {
    final result = await showRaiseHelpDialog(context);
    if (result == null) return;

    try {
      await ref.read(taskRepositoryProvider).raiseEnquiry(order.id, topic: result.topic, message: result.message);
      ref.read(orderHistoryControllerProvider.notifier).refresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(const SnackBar(content: Text('We got your message. Our team will follow up.'), backgroundColor: Colors.green));
      }
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: Colors.red));
      }
    }
  }
}
