import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/theme/app_colors.dart';
import '../../common/widgets/inline_error.dart';

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
        Text('Enter the code', style: Theme.of(context).textTheme.headlineMedium),
        const SizedBox(height: 6),
        Wrap(
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text('Sent to +91 ${widget.state.phone}. ', style: const TextStyle(color: AppColors.muted)),
            GestureDetector(
              onTap: controller.useAnotherNumber,
              child: const Text('Change number', style: TextStyle(color: AppColors.accent, fontWeight: FontWeight.w700)),
            ),
          ],
        ),
        if (widget.state.devCodeHint != null) ...[
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.warningSoft,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              'No SMS provider is set up yet — for now, the code is ${widget.state.devCodeHint}.',
              style: const TextStyle(color: AppColors.warningText, fontWeight: FontWeight.w600),
            ),
          ),
        ],
        const SizedBox(height: 16),
        TextField(
          controller: _codeController,
          keyboardType: TextInputType.number,
          maxLength: 6,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: 8),
          decoration: const InputDecoration(labelText: '6-digit code', counterText: ''),
        ),
        if (widget.state.isNewAccount) ...[
          const SizedBox(height: 12),
          TextField(
            controller: _nameController,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'Your name'),
          ),
          const SizedBox(height: 4),
          const Text(
            'This number has no account yet. We need your name to set one up.',
            style: TextStyle(color: AppColors.muted, fontSize: 12.5),
          ),
        ],
        if (widget.state.error != null) ...[
          const SizedBox(height: 8),
          InlineError(message: widget.state.error!),
        ],
        const SizedBox(height: 16),
        PrimaryButton(
          label: 'Verify & continue',
          isLoading: widget.state.isLoading,
          onPressed: () {
            final code = _codeController.text.trim();
            if (code.length != 6) return;
            final name = _nameController.text.trim();
            controller.verifyOtp(code, name: name.isEmpty ? null : name);
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
