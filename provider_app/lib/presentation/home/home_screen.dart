import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../../domain/entities/app_user.dart';
import '../../domain/entities/provider_dashboard.dart';
import '../../domain/entities/wallet_summary.dart';
import '../common/widgets/status_chip.dart';
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
          ..showSnackBar(SnackBar(content: Text(error), backgroundColor: AppColors.dangerText));
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
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 32),
      children: [
        Text('Hello, ${user.name}', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(profile.skillCategoryName, style: const TextStyle(color: AppColors.muted, fontWeight: FontWeight.w500)),
        const SizedBox(height: 18),
        if (data.offer != null && data.offer!.isLive) ...[
          OfferBanner(offer: data.offer!),
          const SizedBox(height: 16),
        ],
        _DutyToggle(
          isAvailable: profile.isAvailable,
          isBusy: isBusy,
          onTap: () => actions.run(() => ref.read(partnerRepositoryProvider).setAvailability(!profile.isAvailable)),
        ),
        const SizedBox(height: 14),
        Row(
          children: [
            Expanded(child: _StatCard(label: 'Active jobs', value: '${openJobs.length}', icon: Icons.work_outline_rounded)),
            const SizedBox(width: 12),
            Expanded(
              child: wallet.when(
                loading: () => const _StatCard(label: 'This month', value: '…', icon: Icons.currency_rupee_rounded),
                error: (_, _) => const _StatCard(label: 'This month', value: '—', icon: Icons.currency_rupee_rounded),
                data: (w) => _StatCard(
                  label: 'This month',
                  value: '₹${w.earnedThisMonth.round()}',
                  icon: Icons.currency_rupee_rounded,
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
                  StatusChip(
                    label: profile.kycStatus == 'rejected' ? 'KYC rejected' : 'KYC pending',
                    tone: profile.kycStatus == 'rejected' ? StatusTone.danger : StatusTone.warning,
                  ),
                  const SizedBox(height: 12),
                  Text(
                    profile.kycStatus == 'rejected'
                        ? 'Your KYC was rejected'
                        : profile.kycStatus == 'pending'
                            ? 'Your documents are under review'
                            : 'Your KYC is incomplete',
                    style: Theme.of(context).textTheme.titleMedium,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'An administrator checks your KYC before you can see and bid on work.',
                    style: TextStyle(color: AppColors.muted),
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          )
        else
          InkWell(
            onTap: onSeeAvailableWork,
            borderRadius: BorderRadius.circular(22),
            child: Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                gradient: AppColors.brandGradient,
                borderRadius: BorderRadius.circular(22),
                boxShadow: AppColors.accentGlow,
              ),
              child: Row(
                children: [
                  Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.16),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: const Icon(Icons.search_rounded, color: Colors.white),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${data.availableTasks.length} job${data.availableTasks.length == 1 ? '' : 's'} available',
                          style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 17),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          'In ${profile.skillCategoryName}. Tap to browse and bid',
                          style: const TextStyle(color: Color(0xCCFFFFFF), fontSize: 12.5),
                        ),
                      ],
                    ),
                  ),
                  const Icon(Icons.arrow_forward_rounded, color: Colors.white),
                ],
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
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.line),
        boxShadow: AppColors.softShadow,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(color: AppColors.accentSoft, borderRadius: BorderRadius.circular(12)),
            child: Icon(icon, color: AppColors.accent, size: 20),
          ),
          const SizedBox(height: 12),
          Text(value, style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 2),
          Text(label, style: const TextStyle(fontSize: 12.5, color: AppColors.muted, fontWeight: FontWeight.w500)),
        ],
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
    return InkWell(
      onTap: isBusy ? null : onTap,
      borderRadius: BorderRadius.circular(22),
      child: Container(
        padding: const EdgeInsets.fromLTRB(18, 14, 12, 14),
        decoration: BoxDecoration(
          color: isAvailable ? AppColors.ink : AppColors.surface,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(color: isAvailable ? AppColors.ink : AppColors.line),
          boxShadow: isAvailable ? AppColors.liftedShadow : AppColors.softShadow,
        ),
        child: Row(
          children: [
            Container(
              width: 12,
              height: 12,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: isAvailable ? const Color(0xFF34D399) : AppColors.faint,
                boxShadow: isAvailable
                    ? [BoxShadow(color: const Color(0xFF34D399).withValues(alpha: 0.6), blurRadius: 10)]
                    : null,
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    isAvailable ? 'You are on duty' : 'You are off duty',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: isAvailable ? Colors.white : AppColors.ink,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    isAvailable ? 'New jobs can be offered to you' : 'Switch on to receive new jobs',
                    style: TextStyle(fontSize: 12.5, color: isAvailable ? const Color(0xB3FFFFFF) : AppColors.muted),
                  ),
                ],
              ),
            ),
            if (isBusy)
              const Padding(
                padding: EdgeInsets.only(right: 12),
                child: SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2)),
              )
            else
              Switch(value: isAvailable, onChanged: (_) => onTap()),
          ],
        ),
      ),
    );
  }
}
