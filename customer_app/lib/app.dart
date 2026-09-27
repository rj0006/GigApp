import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/theme/app_theme.dart';
import 'domain/entities/app_user.dart';
import 'presentation/auth/screens/login_screen.dart';
import 'presentation/auth/session_controller.dart';
import 'presentation/providers.dart';
import 'presentation/shell/app_shell.dart';
import 'presentation/splash/splash_screen.dart';
import 'presentation/zone/location_picker_screen.dart';

class CustomerApp extends ConsumerWidget {
  const CustomerApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionControllerProvider);

    return MaterialApp(
      title: 'GigApp',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      home: session.when(
        loading: () => const SplashScreen(),
        error: (_, _) => const LoginScreen(),
        data: (user) => user == null ? const LoginScreen() : _PostLoginGate(user: user),
      ),
    );
  }
}

class _PostLoginGate extends ConsumerWidget {
  const _PostLoginGate({required this.user});

  final AppUser user;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final zone = ref.watch(zoneControllerProvider);

    return zone.when(
      loading: () => const SplashScreen(),
      error: (_, _) => const LocationPickerScreen(),
      data: (selected) => selected == null ? const LocationPickerScreen() : AppShell(user: user),
    );
  }
}
