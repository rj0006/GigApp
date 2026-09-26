import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/theme/app_theme.dart';
import 'presentation/auth/screens/login_screen.dart';
import 'presentation/auth/session_controller.dart';
import 'presentation/shell/app_shell.dart';
import 'presentation/splash/splash_screen.dart';

class ProviderApp extends ConsumerWidget {
  const ProviderApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionControllerProvider);

    return MaterialApp(
      title: 'GigApp Partner',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      home: session.when(
        loading: () => const SplashScreen(),
        error: (_, _) => const LoginScreen(),
        data: (user) => user == null ? const LoginScreen() : AppShell(user: user),
      ),
    );
  }
}
