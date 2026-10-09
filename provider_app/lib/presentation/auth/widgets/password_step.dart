import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/theme/app_colors.dart';
import '../../common/widgets/inline_error.dart';

import '../../common/widgets/primary_button.dart';
import '../login_flow_controller.dart';
import '../login_flow_state.dart';

class PasswordStep extends ConsumerStatefulWidget {
  const PasswordStep({super.key, required this.state});

  final LoginFlowState state;

  @override
  ConsumerState<PasswordStep> createState() => _PasswordStepState();
}

class _PasswordStepState extends ConsumerState<PasswordStep> {
  final _passwordController = TextEditingController();
  bool _obscure = true;

  @override
  void dispose() {
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = ref.read(loginFlowControllerProvider.notifier);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Enter your password', style: Theme.of(context).textTheme.headlineMedium),
        const SizedBox(height: 6),
        Wrap(
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text('Signing in as +91 ${widget.state.phone}. ', style: const TextStyle(color: AppColors.muted)),
            GestureDetector(
              onTap: controller.useCodeInstead,
              child: const Text('Use a code instead', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.w700)),
            ),
          ],
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _passwordController,
          obscureText: _obscure,
          decoration: InputDecoration(
            labelText: 'Password',
            suffixIcon: IconButton(
              icon: Icon(_obscure ? Icons.visibility_off_outlined : Icons.visibility_outlined),
              onPressed: () => setState(() => _obscure = !_obscure),
            ),
          ),
        ),
        if (widget.state.error != null) ...[
          const SizedBox(height: 8),
          InlineError(message: widget.state.error!),
        ],
        const SizedBox(height: 16),
        PrimaryButton(
          label: 'Sign in',
          isLoading: widget.state.isLoading,
          onPressed: () => controller.signInWithPassword(_passwordController.text),
        ),
      ],
    );
  }
}
