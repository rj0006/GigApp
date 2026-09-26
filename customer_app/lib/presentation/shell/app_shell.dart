import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/app_user.dart';
import '../cart/widgets/cart_button.dart';
import '../home/home_screen.dart';
import '../notifications/widgets/notification_bell.dart';
import '../orders/orders_screen.dart';
import '../profile/profile_screen.dart';
import '../providers.dart';

const _titles = ['Home', 'Orders', 'Profile'];

final shellSectionProvider = StateProvider.autoDispose<int>((ref) => 0);

class AppShell extends ConsumerStatefulWidget {
  const AppShell({super.key, required this.user});

  final AppUser user;

  @override
  ConsumerState<AppShell> createState() => _AppShellState();
}

class _AppShellState extends ConsumerState<AppShell> {
  @override
  void initState() {
    super.initState();
    ref.read(realtimeClientProvider).connect();
  }

  @override
  Widget build(BuildContext context) {
    ref.watch(realtimeClientProvider);
    ref.listen(notificationToastProvider, (previous, next) {
      if (next == null) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(next), backgroundColor: const Color(0xFF4F46E5)));
    });

    final section = ref.watch(shellSectionProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[section]),
        actions: const [CartButton(), NotificationBell()],
      ),
      body: IndexedStack(
        index: section,
        children: [
          HomeScreen(
            user: widget.user,
            onSeeOrders: () => ref.read(shellSectionProvider.notifier).state = 1,
          ),
          const OrdersScreen(),
          ProfileScreen(user: widget.user),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: section,
        onDestinationSelected: (index) => ref.read(shellSectionProvider.notifier).state = index,
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home), label: 'Home'),
          NavigationDestination(
            icon: Icon(Icons.assignment_outlined),
            selectedIcon: Icon(Icons.assignment),
            label: 'Orders',
          ),
          NavigationDestination(icon: Icon(Icons.person_outline), selectedIcon: Icon(Icons.person), label: 'Profile'),
        ],
      ),
    );
  }
}
