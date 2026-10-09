import 'package:flutter/material.dart';

import 'screens/login_screen.dart';

Future<void> openLogin(BuildContext context) {
  return Navigator.of(context).push(
    MaterialPageRoute<void>(fullscreenDialog: true, builder: (_) => const LoginScreen()),
  );
}
