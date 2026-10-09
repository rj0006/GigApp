import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../domain/entities/app_user.dart';
import '../../auth/session_controller.dart';
import '../../common/widgets/brand_mark.dart';
import '../../common/widgets/confirm_dialog.dart';

class AppDrawer extends ConsumerWidget {
  const AppDrawer({super.key, required this.user, required this.selectedIndex, required this.onSelect});

  final AppUser user;
  final int selectedIndex;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Drawer(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _DrawerHeader(user: user),
          const SizedBox(height: 12),
          _DrawerItem(icon: Icons.home_outlined, label: 'Home', index: 0, selectedIndex: selectedIndex, onSelect: onSelect),
          _DrawerItem(
            icon: Icons.assignment_outlined,
            label: 'Orders',
            index: 1,
            selectedIndex: selectedIndex,
            onSelect: onSelect,
          ),
          _DrawerItem(
            icon: Icons.account_balance_wallet_outlined,
            label: 'Wallet',
            index: 2,
            selectedIndex: selectedIndex,
            onSelect: onSelect,
          ),
          _DrawerItem(
            icon: Icons.person_outline_rounded,
            label: 'Profile',
            index: 3,
            selectedIndex: selectedIndex,
            onSelect: onSelect,
          ),
          const Spacer(),
          const Divider(),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 4, 12, 8),
              child: ListTile(
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                leading: const Icon(Icons.logout_rounded, color: AppColors.dangerText),
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
            ),
          ),
        ],
      ),
    );
  }
}

class _DrawerHeader extends StatelessWidget {
  const _DrawerHeader({required this.user});

  final AppUser user;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.fromLTRB(20, MediaQuery.of(context).padding.top + 20, 20, 22),
      decoration: const BoxDecoration(
        color: AppColors.ink,
        borderRadius: BorderRadius.only(bottomRight: Radius.circular(28)),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF1B1F4B), AppColors.ink],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const BrandMark(size: 30, light: true),
          const SizedBox(height: 24),
          Row(
            children: [
              CircleAvatar(
                radius: 24,
                backgroundColor: Colors.white,
                child: Text(
                  user.name.isEmpty ? '?' : user.name[0].toUpperCase(),
                  style: const TextStyle(
                    fontFamily: AppTheme.displayFont,
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: AppColors.accent,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      user.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 16),
                    ),
                    Text(user.phone, style: const TextStyle(color: Color(0xB3FFFFFF), fontSize: 12.5)),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _DrawerItem extends StatelessWidget {
  const _DrawerItem({
    required this.icon,
    required this.label,
    required this.index,
    required this.selectedIndex,
    required this.onSelect,
  });

  final IconData icon;
  final String label;
  final int index;
  final int selectedIndex;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    final isSelected = index == selectedIndex;
    final color = isSelected ? AppColors.accent : AppColors.inkSoft;

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 2),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(borderRadius: BorderRadius.circular(14)),
      child: Material(
        color: isSelected ? AppColors.accentSoft : Colors.transparent,
        child: ListTile(
          leading: Icon(icon, color: color),
          title: Text(label, style: TextStyle(color: color, fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500)),
          onTap: () => onSelect(index),
        ),
      ),
    );
  }
}
