import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/entities/app_user.dart';
import '../../auth/session_controller.dart';
import '../../common/widgets/confirm_dialog.dart';

class AppDrawer extends ConsumerWidget {
  const AppDrawer({super.key, required this.user, required this.selectedIndex, required this.onSelect});

  final AppUser user;
  final int selectedIndex;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Drawer(
      child: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            DrawerHeader(
              decoration: const BoxDecoration(color: Color(0xFF4F46E5)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  CircleAvatar(
                    radius: 24,
                    backgroundColor: Colors.white,
                    child: Text(
                      user.name.isEmpty ? '?' : user.name[0].toUpperCase(),
                      style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: Color(0xFF4F46E5)),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Text(user.name, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 16)),
                  Text(user.phone, style: const TextStyle(color: Colors.white70, fontSize: 12)),
                ],
              ),
            ),
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
              icon: Icons.person_outline,
              label: 'Profile',
              index: 3,
              selectedIndex: selectedIndex,
              onSelect: onSelect,
            ),
            const Spacer(),
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
            const SizedBox(height: 8),
          ],
        ),
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
    final color = isSelected ? const Color(0xFF4F46E5) : Colors.grey.shade700;

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(borderRadius: BorderRadius.circular(10)),
      child: Material(
        color: isSelected ? const Color(0xFF4F46E5).withValues(alpha: 0.08) : Colors.transparent,
        child: ListTile(
          leading: Icon(icon, color: color),
          title: Text(label, style: TextStyle(color: color, fontWeight: isSelected ? FontWeight.w600 : FontWeight.normal)),
          onTap: () => onSelect(index),
        ),
      ),
    );
  }
}
