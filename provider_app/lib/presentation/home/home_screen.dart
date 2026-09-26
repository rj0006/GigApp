import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/app_user.dart';
import '../../domain/entities/provider_dashboard.dart';
import '../../domain/entities/wallet_summary.dart';
import '../dashboard/dashboard_actions_controller.dart';
import '../providers.dart';
import 'widgets/offer_banner.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key, required this.user, required this.onSeeAvailableWork});

  final AppUser user;
  final VoidCallback onSeeAvailableWork;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboard = ref.watch(dashboardProvider);
    final wallet = ref.watch(walletSummaryProvider);

    ref.listen(dashboardActionsControllerProvider, (previous, next) {
      final error = next.hasError ? (next.error).toString() : null;
      if (error != null) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(error), backgroundColor: Colors.red));
      }
    });

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(dashboardProvider);
        ref.invalidate(walletSummaryProvider);
      },
      child: dashboard.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ListView(
          children: [
            const SizedBox(height: 80),
            Center(child: Text('Could not load your dashboard.\n$error', textAlign: TextAlign.center)),
          ],
        ),
        data: (data) => _HomeBody(user: user, data: data, wallet: wallet, onSeeAvailableWork: onSeeAvailableWork),
      ),
    );
  }
}

class _HomeBody extends ConsumerWidget {
  const _HomeBody({required this.user, required this.data, required this.wallet, required this.onSeeAvailableWork});

  final AppUser user;
  final ProviderDashboard data;
  final AsyncValue<WalletSummary> wallet;
  final VoidCallback onSeeAvailableWork;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = data.profile;

    if (profile == null) {
      return ListView(
        padding: const EdgeInsets.all(16),
        children: const [
          SizedBox(height: 60),
          Center(child: Text('No partner profile is attached to this account.')),
        ],
      );
    }

    final actions = ref.read(dashboardActionsControllerProvider.notifier);
    final isBusy = ref.watch(dashboardActionsControllerProvider).isLoading;

    final openJobs = data.myJobs.where((j) => ['pending', 'accepted', 'in_progress'].contains(j.status)).toList();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Hello, ${user.name}', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        if (data.offer != null && data.offer!.isLive) ...[
          OfferBanner(offer: data.offer!),
          const SizedBox(height: 16),
        ],
        _DutyToggle(
          isAvailable: profile.isAvailable,
          isBusy: isBusy,
          onTap: () => actions.run(() => ref.read(partnerRepositoryProvider).setAvailability(!profile.isAvailable)),
        ),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(child: _StatCard(label: 'Active jobs', value: '${openJobs.length}', icon: Icons.work_outline)),
            const SizedBox(width: 10),
            Expanded(
              child: wallet.when(
                loading: () => const _StatCard(label: 'This month', value: '…', icon: Icons.currency_rupee),
                error: (_, _) => const _StatCard(label: 'This month', value: '—', icon: Icons.currency_rupee),
                data: (w) => _StatCard(
                  label: 'This month',
                  value: '₹${w.earnedThisMonth.round()}',
                  icon: Icons.currency_rupee,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 20),
        if (!profile.isVerified)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                children: [
                  Text(
                    profile.kycStatus == 'rejected'
                        ? 'Your KYC was rejected'
                        : profile.kycStatus == 'pending'
                            ? 'Your documents are under review'
                            : 'Your KYC is incomplete',
                    style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'An administrator checks your KYC before you can see and bid on work.',
                    style: TextStyle(color: Colors.grey.shade600),
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          )
        else
          Card(
            child: InkWell(
              onTap: onSeeAvailableWork,
              borderRadius: BorderRadius.circular(14),
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  children: [
                    const Icon(Icons.search, color: Color(0xFF4F46E5)),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            '${data.availableTasks.length} jobs available in ${profile.skillCategoryName}',
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          Text('Tap to browse and bid', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                        ],
                      ),
                    ),
                    const Icon(Icons.chevron_right),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value, required this.icon});

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Icon(icon, color: const Color(0xFF4F46E5)),
            const SizedBox(width: 10),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(value, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                Text(label, style: TextStyle(fontSize: 11, color: Colors.grey.shade500)),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _DutyToggle extends StatelessWidget {
  const _DutyToggle({required this.isAvailable, required this.isBusy, required this.onTap});

  final bool isAvailable;
  final bool isBusy;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = isAvailable ? Colors.green : Colors.grey;

    return Card(
      child: InkWell(
        onTap: isBusy ? null : onTap,
        borderRadius: BorderRadius.circular(14),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(20)),
                child: Text(
                  isAvailable ? 'On duty' : 'Off duty',
                  style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 12),
                ),
              ),
              const SizedBox(width: 10),
              Text('Availability — tap to change', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
              const Spacer(),
              if (isBusy) const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)),
            ],
          ),
        ),
      ),
    );
  }
}
