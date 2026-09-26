import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/bid.dart';
import '../../domain/entities/gig_task.dart';
import '../../domain/entities/provider_dashboard.dart';
import '../common/widgets/confirm_dialog.dart';
import '../dashboard/dashboard_actions_controller.dart';
import '../dashboard/widgets/available_task_card.dart';
import '../dashboard/widgets/bid_dialog.dart';
import '../dashboard/widgets/cancel_job_dialog.dart';
import '../dashboard/widgets/complete_job_dialog.dart';
import '../dashboard/widgets/my_bid_card.dart';
import '../dashboard/widgets/my_job_card.dart';
import '../providers.dart';

class OrdersScreen extends ConsumerStatefulWidget {
  const OrdersScreen({super.key, this.initialTab = 0});

  final int initialTab;

  @override
  ConsumerState<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends ConsumerState<OrdersScreen> with SingleTickerProviderStateMixin {
  late final TabController _tabController =
      TabController(length: 3, vsync: this, initialIndex: widget.initialTab);

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final dashboard = ref.watch(dashboardProvider);

    ref.listen(dashboardActionsControllerProvider, (previous, next) {
      final error = next.hasError ? (next.error).toString() : null;
      if (error != null) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(error), backgroundColor: Colors.red));
      }
    });

    return Column(
      children: [
        TabBar(
          controller: _tabController,
          labelColor: const Color(0xFF4F46E5),
          unselectedLabelColor: Colors.grey,
          tabs: const [
            Tab(text: 'Available'),
            Tab(text: 'My bids'),
            Tab(text: 'My jobs'),
          ],
        ),
        Expanded(
          child: dashboard.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => Center(child: Text('Could not load.\n$error', textAlign: TextAlign.center)),
            data: (data) => TabBarView(
              controller: _tabController,
              children: [
                _AvailableTab(data: data),
                _BidsTab(data: data),
                _JobsTab(jobs: data.myJobs),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _AvailableTab extends ConsumerWidget {
  const _AvailableTab({required this.data});

  final ProviderDashboard data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = data.profile;
    final actions = ref.read(dashboardActionsControllerProvider.notifier);
    final isBusy = ref.watch(dashboardActionsControllerProvider).isLoading;

    if (profile != null && !profile.isVerified) {
      return _emptyHint('Available work is hidden until your KYC is approved.');
    }

    return RefreshIndicator(
      onRefresh: () => ref.refresh(dashboardProvider.future),
      child: data.availableTasks.isEmpty
          ? ListView(children: [_emptyHint('Nothing new in your category right now.')])
          : ListView(
              padding: const EdgeInsets.all(16),
              children: data.availableTasks
                  .map(
                    (task) => AvailableTaskCard(
                      task: task,
                      onAct: isBusy ? () {} : () => _actOnAvailableTask(context, ref, task, actions),
                    ),
                  )
                  .toList(),
            ),
    );
  }

  Future<void> _actOnAvailableTask(
    BuildContext context,
    WidgetRef ref,
    GigTask task,
    DashboardActionsController actions,
  ) async {
    if (task.isInstant) {
      final confirmed = await showConfirmDialog(
        context,
        title: 'Take this job?',
        message: 'You will earn ₹${task.effectiveAmount.round()} before commission for '
            '${task.serviceItemName ?? task.categoryName} at ${task.address}. The price is fixed and '
            'the job becomes yours immediately.',
        confirmLabel: 'Yes, take it',
      );
      if (!confirmed) return;
      final ok = await actions.run(() => ref.read(taskRepositoryProvider).acceptInstantTask(task.id));
      if (ok && context.mounted) _toast(context, 'Job #${task.id} is yours. It is now under My jobs.');
      return;
    }

    if (!context.mounted) return;
    final result = await showBidDialog(
      context,
      title: 'Bid on task #${task.id}',
      summary: '${task.description}\n${task.address} · customer\'s budget ₹${task.budget.round()}',
      initialAmount: task.budget,
    );
    if (result == null) return;

    final ok = await actions.run(
      () => ref.read(bidRepositoryProvider).placeBid(taskId: task.id, amount: result.amount, note: result.note),
    );
    if (ok && context.mounted) {
      _toast(context, 'Bid of ₹${result.amount.round()} placed on task #${task.id}.');
    }
  }
}

class _BidsTab extends ConsumerWidget {
  const _BidsTab({required this.data});

  final ProviderDashboard data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final actions = ref.read(dashboardActionsControllerProvider.notifier);
    final isBusy = ref.watch(dashboardActionsControllerProvider).isLoading;

    return RefreshIndicator(
      onRefresh: () => ref.refresh(dashboardProvider.future),
      child: data.myBids.isEmpty
          ? ListView(children: [_emptyHint('You have not bid on anything yet.')])
          : ListView(
              padding: const EdgeInsets.all(16),
              children: data.myBids
                  .map(
                    (bid) => MyBidCard(
                      bid: bid,
                      onAcceptCounter: isBusy ? () {} : () => _acceptCounter(context, ref, bid, actions),
                      onChangeBid: isBusy ? () {} : () => _changeBid(context, ref, bid, actions),
                      onWithdraw: isBusy ? () {} : () => _withdrawBid(context, ref, bid, actions),
                    ),
                  )
                  .toList(),
            ),
    );
  }

  Future<void> _changeBid(BuildContext context, WidgetRef ref, Bid bid, DashboardActionsController actions) async {
    final result = await showBidDialog(
      context,
      title: 'Change your bid · task #${bid.gigTaskId}',
      summary: 'Customer\'s budget is ₹${bid.taskBudget?.round() ?? 0}.'
          '${bid.counterAmount != null ? ' They countered at ₹${bid.counterAmount!.round()}.' : ''}',
      initialAmount: bid.amount,
      initialNote: bid.note,
      submitLabel: 'Update bid',
    );
    if (result == null) return;

    final ok = await actions.run(
      () => ref.read(bidRepositoryProvider).placeBid(taskId: bid.gigTaskId, amount: result.amount, note: result.note),
    );
    if (ok && context.mounted) _toast(context, 'Bid updated.');
  }

  Future<void> _withdrawBid(BuildContext context, WidgetRef ref, Bid bid, DashboardActionsController actions) async {
    final confirmed = await showConfirmDialog(context, title: 'Withdraw your bid?', destructive: true);
    if (!confirmed) return;

    final ok = await actions.run(() => ref.read(bidRepositoryProvider).withdrawBid(bid.id));
    if (ok && context.mounted) _toast(context, 'Bid withdrawn.');
  }

  Future<void> _acceptCounter(BuildContext context, WidgetRef ref, Bid bid, DashboardActionsController actions) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Accept ₹${bid.currentAmount.round()}?',
      message: 'The job becomes yours immediately.',
      confirmLabel: 'Yes, accept',
    );
    if (!confirmed) return;

    final ok = await actions.run(() => ref.read(bidRepositoryProvider).acceptCounter(bid.id));
    if (ok && context.mounted) _toast(context, 'Counter offer accepted. The job is now yours.');
  }
}

enum _JobFilter { active, completed, cancelled }

const _activeStatuses = ['pending', 'accepted', 'in_progress'];

class _JobsTab extends ConsumerStatefulWidget {
  const _JobsTab({required this.jobs});

  final List<GigTask> jobs;

  @override
  ConsumerState<_JobsTab> createState() => _JobsTabState();
}

class _JobsTabState extends ConsumerState<_JobsTab> {
  _JobFilter _filter = _JobFilter.active;

  @override
  Widget build(BuildContext context) {
    final actions = ref.read(dashboardActionsControllerProvider.notifier);
    final isBusy = ref.watch(dashboardActionsControllerProvider).isLoading;

    final filtered = widget.jobs.where((job) {
      switch (_filter) {
        case _JobFilter.active:
          return _activeStatuses.contains(job.status);
        case _JobFilter.completed:
          return job.status == 'completed';
        case _JobFilter.cancelled:
          return job.status == 'cancelled';
      }
    }).toList();

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Row(
            children: [
              _FilterChip(
                label: 'Active',
                selected: _filter == _JobFilter.active,
                onTap: () => setState(() => _filter = _JobFilter.active),
              ),
              const SizedBox(width: 8),
              _FilterChip(
                label: 'Completed',
                selected: _filter == _JobFilter.completed,
                onTap: () => setState(() => _filter = _JobFilter.completed),
              ),
              const SizedBox(width: 8),
              _FilterChip(
                label: 'Cancelled',
                selected: _filter == _JobFilter.cancelled,
                onTap: () => setState(() => _filter = _JobFilter.cancelled),
              ),
            ],
          ),
        ),
        Expanded(
          child: RefreshIndicator(
            onRefresh: () => ref.refresh(dashboardProvider.future),
            child: filtered.isEmpty
                ? ListView(children: [_emptyHint('Nothing here yet.')])
                : ListView(
                    padding: const EdgeInsets.all(16),
                    children: filtered
                        .map(
                          (job) => MyJobCard(
                            job: job,
                            onStart: isBusy ? () {} : () => _startJob(context, ref, job, actions),
                            onComplete: isBusy ? () {} : () => _completeJob(context, ref, job, actions),
                            onCancel: isBusy ? () {} : () => _cancelJob(context, ref, job, actions),
                          ),
                        )
                        .toList(),
                  ),
          ),
        ),
      ],
    );
  }

  Future<void> _startJob(BuildContext context, WidgetRef ref, GigTask job, DashboardActionsController actions) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Start this job?',
      message: 'Start only once you have reached ${job.address}. The customer is told the work has begun.',
      confirmLabel: 'Yes, I have reached',
    );
    if (!confirmed) return;

    final ok = await actions.run(() => ref.read(taskRepositoryProvider).updateStatus(job.id, status: 'in_progress'));
    if (ok && context.mounted) _toast(context, 'Task #${job.id} is now in progress.');
  }

  Future<void> _completeJob(BuildContext context, WidgetRef ref, GigTask job, DashboardActionsController actions) async {
    final result = await showCompleteJobDialog(
      context,
      title: 'Close job #${job.id}',
      summary: '₹${job.effectiveAmount.round()} will be credited to your balance and the customer will '
          'be asked to rate you. This cannot be undone.',
      customerName: job.customerName ?? 'this customer',
    );
    if (result == null) return;

    final ok = await actions.run(
      () => ref.read(taskRepositoryProvider).updateStatus(
            job.id,
            status: 'completed',
            stars: result.stars,
            feedback: result.feedback,
          ),
    );
    if (ok && context.mounted) {
      _toast(context, 'Task #${job.id} is complete. Your earning has been added to your balance.');
    }
  }

  Future<void> _cancelJob(BuildContext context, WidgetRef ref, GigTask job, DashboardActionsController actions) async {
    final reason = await showCancelJobDialog(
      context,
      title: 'Cancel job #${job.id}',
      summary: 'This job goes back to other partners in the same category. You will not be offered it '
          'again, and this cannot be undone.',
    );
    if (reason == null) return;

    final ok =
        await actions.run(() => ref.read(taskRepositoryProvider).updateStatus(job.id, status: 'cancelled', cancelReason: reason));
    if (ok && context.mounted) _toast(context, 'Task #${job.id} cancelled. It has been reopened for other partners.');
  }
}

class _FilterChip extends StatelessWidget {
  const _FilterChip({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ChoiceChip(
      label: Text(label),
      selected: selected,
      onSelected: (_) => onTap(),
      selectedColor: const Color(0xFF4F46E5).withValues(alpha: 0.15),
      labelStyle: TextStyle(
        color: selected ? const Color(0xFF4F46E5) : Colors.grey.shade700,
        fontWeight: selected ? FontWeight.w600 : FontWeight.normal,
      ),
      side: BorderSide(color: selected ? const Color(0xFF4F46E5) : Colors.grey.shade300),
    );
  }
}

Widget _emptyHint(String text) {
  return Padding(
    padding: const EdgeInsets.symmetric(vertical: 40, horizontal: 20),
    child: Center(child: Text(text, style: TextStyle(color: Colors.grey.shade600), textAlign: TextAlign.center)),
  );
}

void _toast(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message), backgroundColor: Colors.green));
}
