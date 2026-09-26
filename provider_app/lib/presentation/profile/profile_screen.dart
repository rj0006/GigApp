import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
                  leading: const Icon(Icons.logout, color: Colors.red),
                  title: const Text('Sign out', style: TextStyle(color: Colors.red)),
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
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            Align(
              alignment: Alignment.topRight,
              child: IconButton(
                icon: const Icon(Icons.edit_outlined),
                tooltip: 'Edit profile',
                onPressed: onEdit,
              ),
            ),
            CircleAvatar(
              radius: 32,
              backgroundColor: const Color(0xFF4F46E5).withValues(alpha: 0.12),
              child: Text(
                user.name.isEmpty ? '?' : user.name[0].toUpperCase(),
                style: const TextStyle(fontSize: 26, fontWeight: FontWeight.bold, color: Color(0xFF4F46E5)),
              ),
            ),
            const SizedBox(height: 12),
            Text(user.name, style: Theme.of(context).textTheme.titleLarge),
            Text(user.phone, style: TextStyle(color: Colors.grey.shade600)),
            if (user.email != null) Text(user.email!, style: TextStyle(color: Colors.grey.shade600)),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(child: _statTile('Skill', profile.skillCategoryName)),
                Expanded(
                  child: _statTile(
                    'Rating',
                    profile.averageRating == null ? '—' : '${profile.averageRating!.toStringAsFixed(1)} ★',
                  ),
                ),
                Expanded(
                  child: _statTile(
                    'KYC',
                    profile.kycLabel,
                    color: profile.isVerified ? Colors.green : Colors.orange,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _statTile(String label, String value, {Color? color}) {
    return Column(
      children: [
        Text(value, style: TextStyle(fontWeight: FontWeight.w700, color: color)),
        const SizedBox(height: 2),
        Text(label, style: TextStyle(fontSize: 11, color: Colors.grey.shade500)),
      ],
    );
  }
}
