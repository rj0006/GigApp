import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../common/widgets/primary_button.dart';
import '../login_flow_controller.dart';
import '../login_flow_state.dart';
import 'more_options_sheet.dart';

class CodeStep extends ConsumerStatefulWidget {
  const CodeStep({super.key, required this.state});

  final LoginFlowState state;

  @override
  ConsumerState<CodeStep> createState() => _CodeStepState();
}

class _CodeStepState extends ConsumerState<CodeStep> {
  final _codeController = TextEditingController();
  final _nameController = TextEditingController();

  @override
  void dispose() {
    _codeController.dispose();
    _nameController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = ref.read(loginFlowControllerProvider.notifier);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Enter the code', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 6),
        Wrap(
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text('Sent to +91 ${widget.state.phone}. ', style: TextStyle(color: Colors.grey.shade600)),
            GestureDetector(
              onTap: controller.useAnotherNumber,
              child: const Text('Change number', style: TextStyle(decoration: TextDecoration.underline)),
            ),
          ],
        ),
        if (widget.state.devCodeHint != null) ...[
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: const Color(0xFFFFF3CD),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Text(
              'No SMS provider is set up yet — for now, the code is ${widget.state.devCodeHint}.',
            ),
          ),
        ],
        const SizedBox(height: 16),
        TextField(
          controller: _codeController,
          keyboardType: TextInputType.number,
          maxLength: 6,
          decoration: const InputDecoration(labelText: '6-digit code', counterText: ''),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _nameController,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Your name'),
        ),
        const SizedBox(height: 4),
        Text(
          'Only needed the first time, to set up a new account.',
          style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
        ),
        if (widget.state.error != null) ...[
          const SizedBox(height: 8),
          Text(widget.state.error!, style: const TextStyle(color: Colors.red)),
        ],
        const SizedBox(height: 16),
        PrimaryButton(
          label: 'Verify & continue',
          isLoading: widget.state.isLoading,
          onPressed: () {
            final code = _codeController.text.trim();
            if (code.length == 6) {
              controller.verifyOtp(code, name: _nameController.text.trim());
            }
          },
        ),
        const SizedBox(height: 8),
        TextButton(onPressed: controller.resendOtp, child: const Text('Did not get it? Resend')),
        Center(
          child: TextButton(
            onPressed: () => showMoreOptionsSheet(context, onUsePassword: controller.switchToPassword),
            child: const Text('More options'),
          ),
        ),
      ],
    );
  }
}
