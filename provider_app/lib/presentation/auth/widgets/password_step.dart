import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
        Text('Enter your password', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 6),
        Wrap(
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text('Signing in as +91 ${widget.state.phone}. ', style: TextStyle(color: Colors.grey.shade600)),
            GestureDetector(
              onTap: controller.useCodeInstead,
              child: const Text('Use a code instead', style: TextStyle(decoration: TextDecoration.underline)),
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
              icon: Icon(_obscure ? Icons.visibility_off : Icons.visibility),
              onPressed: () => setState(() => _obscure = !_obscure),
            ),
          ),
        ),
        if (widget.state.error != null) ...[
          const SizedBox(height: 8),
          Text(widget.state.error!, style: const TextStyle(color: Colors.red)),
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
