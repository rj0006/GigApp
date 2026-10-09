import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/theme/app_theme.dart';

import '../../domain/entities/app_user.dart';
import '../../domain/entities/provider_dashboard.dart';
import '../auth/session_controller.dart';
import '../common/widgets/confirm_dialog.dart';
import '../providers.dart';
import '../settings/devices_screen.dart';
import '../settings/service_area_screen.dart';
import 'widgets/bank_account_dialog.dart';
import 'widgets/change_password_dialog.dart';
import 'widgets/edit_profile_dialog.dart';
import '../../core/theme/app_colors.dart';

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key, required this.user});

  final AppUser user;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboard = ref.watch(dashboardProvider);
    final wallet = ref.watch(walletSummaryProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(dashboardProvider);
        ref.invalidate(walletSummaryProvider);
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          dashboard.when(
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: 40),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => Text('Could not load profile.\n$error', textAlign: TextAlign.center),
            data: (data) => data.profile == null
                ? const SizedBox.shrink()
                : _ProfileHeader(
                    user: user,
                    profile: data.profile!,
                    onEdit: () async {
                      final updated = await showEditProfileDialog(context, current: user);
                      if (updated != null) {
                        ref.read(sessionControllerProvider.notifier).setSignedIn(updated);
                        ref.invalidate(dashboardProvider);
                      }
                    },
                  ),
          ),
          const SizedBox(height: 20),
          Text('Bank account', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          wallet.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (_, _) => const SizedBox.shrink(),
            data: (data) => Card(
              child: ListTile(
                leading: const Icon(Icons.account_balance),
                title: Text(data.bankAccount?.bankName ?? 'No bank account added'),
                subtitle: Text(
                  data.bankAccount == null
                      ? 'Add your bank details to receive payouts'
                      : '${data.bankAccount!.accountHolderName} · ${data.bankAccount!.maskedAccountNumber}',
                ),
                trailing: TextButton(
                  onPressed: () async {
                    final saved = await showBankAccountDialog(context, current: data.bankAccount);
                    if (saved) ref.invalidate(walletSummaryProvider);
                  },
                  child: Text(data.bankAccount == null ? 'Add' : 'Edit'),
                ),
              ),
            ),
          ),
          const SizedBox(height: 20),
          Text('Settings', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          Card(
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.map_outlined),
                  title: const Text('Service area'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const ServiceAreaScreen()),
                  ),
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.devices_outlined),
                  title: const Text('Manage devices'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const DevicesScreen()),
                  ),
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.lock_outline),
                  title: const Text('Change password'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => showChangePasswordDialog(context),
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.logout, color: AppColors.dangerText),
                  title: const Text('Sign out', style: TextStyle(color: AppColors.dangerText)),
                  onTap: () async {
                    final confirmed = await showConfirmDialog(
                      context,
                      title: 'Sign out?',
                      confirmLabel: 'Yes, sign out',
                      destructive: true,
                    );
                    if (confirmed) ref.read(sessionControllerProvider.notifier).signOut();
                  },
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({required this.user, required this.profile, required this.onEdit});

  final AppUser user;
  final DashboardPartnerProfile profile;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 8, 8, 22),
      decoration: BoxDecoration(
        gradient: AppColors.brandGradient,
        borderRadius: BorderRadius.circular(26),
        boxShadow: AppColors.accentGlow,
      ),
      child: Column(
        children: [
          Align(
            alignment: Alignment.topRight,
            child: IconButton(
              icon: const Icon(Icons.edit_outlined, color: Colors.white),
              tooltip: 'Edit profile',
              onPressed: onEdit,
            ),
          ),
          Container(
            padding: const EdgeInsets.all(3),
            decoration: BoxDecoration(shape: BoxShape.circle, color: Colors.white.withValues(alpha: 0.35)),
            child: CircleAvatar(
              radius: 34,
              backgroundColor: Colors.white,
              child: Text(
                user.name.isEmpty ? '?' : user.name[0].toUpperCase(),
                style: const TextStyle(
                  fontFamily: AppTheme.displayFont,
                  fontSize: 28,
                  fontWeight: FontWeight.w800,
                  color: AppColors.accent,
                ),
              ),
            ),
          ),
          const SizedBox(height: 14),
          Text(
            user.name,
            style: const TextStyle(
              fontFamily: AppTheme.displayFont,
              fontSize: 22,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.4,
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 2),
          Text(user.phone, style: const TextStyle(color: Color(0xDBFFFFFF), fontWeight: FontWeight.w500)),
          if (user.email != null) Text(user.email!, style: const TextStyle(color: Color(0xB8FFFFFF), fontSize: 13)),
          const SizedBox(height: 18),
          Container(
            margin: const EdgeInsets.only(right: 12),
            padding: const EdgeInsets.symmetric(vertical: 12),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.14),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Row(
              children: [
                Expanded(child: _statTile('Skill', profile.skillCategoryName)),
                Expanded(
                  child: _statTile(
                    'Rating',
                    profile.averageRating == null ? '—' : '${profile.averageRating!.toStringAsFixed(1)} ★',
                  ),
                ),
                Expanded(child: _statTile('KYC', profile.kycLabel)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _statTile(String label, String value) {
    return Column(
      children: [
        Text(value, style: const TextStyle(fontWeight: FontWeight.w800, color: Colors.white)),
        const SizedBox(height: 2),
        Text(label, style: const TextStyle(fontSize: 11, color: Color(0xCCFFFFFF))),
      ],
    );
  }
}
