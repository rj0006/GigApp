import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/app_user.dart';
import '../auth/open_login.dart';
import '../cart/widgets/cart_button.dart';
import '../common/widgets/guest_prompt.dart';
import '../home/home_screen.dart';
import '../notifications/widgets/notification_bell.dart';
import '../orders/orders_screen.dart';
import '../profile/profile_screen.dart';
import '../providers.dart';

const _titles = ['Home', 'Orders', 'Profile'];

final shellSectionProvider = StateProvider.autoDispose<int>((ref) => 0);

class AppShell extends ConsumerStatefulWidget {
  const AppShell({super.key, this.user});

  final AppUser? user;

  @override
  ConsumerState<AppShell> createState() => _AppShellState();
}

class _AppShellState extends ConsumerState<AppShell> {
  bool get _isGuest => widget.user == null;

  @override
  void initState() {
    super.initState();
    if (!_isGuest) ref.read(realtimeClientProvider).connect();
  }

  @override
  Widget build(BuildContext context) {
    if (!_isGuest) ref.watch(realtimeClientProvider);
    ref.listen(notificationToastProvider, (previous, next) {
      if (next == null) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(next)));
    });

    final section = ref.watch(shellSectionProvider);
    final user = widget.user;

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[section]),
        actions: _isGuest
            ? [
                TextButton(onPressed: () => openLogin(context), child: const Text('Sign in')),
                const SizedBox(width: 8),
              ]
            : const [CartButton(), NotificationBell()],
      ),
      body: IndexedStack(
        index: section,
        children: [
          HomeScreen(
            user: user,
            onSeeOrders: () => ref.read(shellSectionProvider.notifier).state = 1,
          ),
          user == null
              ? const GuestPrompt(
                  icon: Icons.assignment_outlined,
                  title: 'Sign in to see your orders',
                  message: 'Book a service and track every order here.',
                )
              : const OrdersScreen(),
          user == null
              ? const GuestPrompt(
                  icon: Icons.person_outline_rounded,
                  title: 'Sign in to your account',
                  message: 'Manage your addresses, bank details and devices.',
                )
              : ProfileScreen(user: user),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: section,
        onDestinationSelected: (index) => ref.read(shellSectionProvider.notifier).state = index,
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home_rounded), label: 'Home'),
          NavigationDestination(
            icon: Icon(Icons.assignment_outlined),
            selectedIcon: Icon(Icons.assignment_rounded),
            label: 'Orders',
          ),
          NavigationDestination(icon: Icon(Icons.person_outline), selectedIcon: Icon(Icons.person_rounded), label: 'Profile'),
        ],
      ),
    );
  }
}
