import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../common/widgets/auth_scaffold.dart';
import '../login_flow_controller.dart';
import '../login_flow_state.dart';
import '../widgets/code_step.dart';
import '../widgets/password_step.dart';
import '../widgets/phone_step.dart';

class LoginScreen extends ConsumerWidget {
  const LoginScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(loginFlowControllerProvider);

    return AuthScaffold(
      portalLabel: 'Customer',
      tagline: 'Trusted home services, booked in minutes.',
      points: const ['Fixed prices', 'Verified professionals', 'Nearest partner'],
      child: AnimatedSwitcher(
        duration: const Duration(milliseconds: 200),
        child: _stepWidget(state),
      ),
    );
  }

  Widget _stepWidget(LoginFlowState state) {
    switch (state.step) {
      case LoginStep.phone:
        return PhoneStep(key: const ValueKey('phone'), state: state);
      case LoginStep.code:
        return CodeStep(key: const ValueKey('code'), state: state);
      case LoginStep.password:
        return PasswordStep(key: const ValueKey('password'), state: state);
    }
  }
}
