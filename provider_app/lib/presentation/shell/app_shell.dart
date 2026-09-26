import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/app_user.dart';
import '../home/home_screen.dart';
import '../kyc/kyc_status_screen.dart';
import '../notifications/widgets/notification_bell.dart';
import '../orders/orders_screen.dart';
import '../profile/profile_screen.dart';
import '../providers.dart';
import '../wallet/wallet_screen.dart';
import 'widgets/app_drawer.dart';

const _titles = ['Home', 'Orders', 'Wallet', 'Profile'];

final shellSectionProvider = StateProvider.autoDispose<int>((ref) => 0);
final ordersInitialTabProvider = StateProvider.autoDispose<int>((ref) => 0);

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

    final dashboard = ref.watch(dashboardProvider);

    return dashboard.when(
      loading: () => const Scaffold(body: Center(child: CircularProgressIndicator())),
      error: (error, _) => Scaffold(
        appBar: AppBar(title: const Text('GigApp Partner')),
        body: Center(child: Text('Could not load your account.\n$error', textAlign: TextAlign.center)),
      ),
      data: (data) {
        final profile = data.profile;
        if (profile != null && !profile.isVerified) {
          return KycStatusScreen(user: widget.user, profile: profile);
        }
        return _VerifiedShell(user: widget.user);
      },
    );
  }
}

class _VerifiedShell extends ConsumerWidget {
  const _VerifiedShell({required this.user});

  final AppUser user;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final section = ref.watch(shellSectionProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[section]),
        actions: const [NotificationBell()],
      ),
      drawer: AppDrawer(
        user: user,
        selectedIndex: section,
        onSelect: (index) {
          ref.read(shellSectionProvider.notifier).state = index;
          Navigator.of(context).pop();
        },
      ),
      body: IndexedStack(
        index: section,
        children: [
          HomeScreen(
            user: user,
            onSeeAvailableWork: () {
              ref.read(ordersInitialTabProvider.notifier).state = 0;
              ref.read(shellSectionProvider.notifier).state = 1;
            },
          ),
          OrdersScreen(key: ValueKey(ref.watch(ordersInitialTabProvider)), initialTab: ref.watch(ordersInitialTabProvider)),
          const WalletScreen(),
          ProfileScreen(user: user),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: section < 2 ? section : 0,
        onDestinationSelected: (index) => ref.read(shellSectionProvider.notifier).state = index,
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home), label: 'Home'),
          NavigationDestination(
            icon: Icon(Icons.assignment_outlined),
            selectedIcon: Icon(Icons.assignment),
            label: 'Orders',
          ),
        ],
      ),
    );
  }
}
