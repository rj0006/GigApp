import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/theme/app_theme.dart';

import '../../domain/entities/app_user.dart';
import '../addresses/addresses_screen.dart';
import '../auth/session_controller.dart';
import '../common/widgets/confirm_dialog.dart';
import '../providers.dart';
import '../settings/devices_screen.dart';
import 'widgets/bank_account_dialog.dart';
import 'widgets/change_password_dialog.dart';
import 'widgets/edit_profile_dialog.dart';
import '../../core/theme/app_colors.dart';

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key, required this.user});

  final AppUser user;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(bankAccountProvider),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _ProfileHeader(
            user: user,
            onEdit: () async {
              final updated = await showEditProfileDialog(context, current: user);
              if (updated != null) {
                ref.read(sessionControllerProvider.notifier).setSignedIn(updated);
              }
            },
          ),
          const SizedBox(height: 20),
          Card(
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.location_on_outlined),
                  title: const Text('My addresses'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const AddressesScreen()),
                  ),
                ),
                const Divider(height: 1),
                Consumer(
                  builder: (context, ref, _) {
                    final bank = ref.watch(bankAccountProvider);
                    return bank.when(
                      loading: () => const ListTile(leading: Icon(Icons.account_balance_outlined), title: Text('Loading…')),
                      error: (_, _) => const ListTile(leading: Icon(Icons.account_balance_outlined), title: Text('Bank account')),
                      data: (account) => ListTile(
                        leading: const Icon(Icons.account_balance_outlined),
                        title: Text(account?.bankName ?? 'Bank account'),
                        subtitle: Text(account == null ? 'Add for refunds and payouts' : account.maskedAccountNumber),
                        trailing: TextButton(
                          onPressed: () async {
                            final saved = await showBankAccountDialog(context, current: account);
                            if (saved) ref.invalidate(bankAccountProvider);
                          },
                          child: Text(account == null ? 'Add' : 'Edit'),
                        ),
                      ),
                    );
                  },
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),
          Text('Settings', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 12),
          Card(
            child: Column(
              children: [
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
  const _ProfileHeader({required this.user, required this.onEdit});

  final AppUser user;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 8, 8, 24),
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
          if (user.email != null)
            Text(user.email!, style: const TextStyle(color: Color(0xB8FFFFFF), fontSize: 13)),
        ],
      ),
    );
  }
}
