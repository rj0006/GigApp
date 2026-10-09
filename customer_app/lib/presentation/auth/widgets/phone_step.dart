import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/theme/app_colors.dart';
import '../../common/widgets/inline_error.dart';

import '../../common/widgets/primary_button.dart';
import '../login_flow_controller.dart';
import '../login_flow_state.dart';

class PhoneStep extends ConsumerStatefulWidget {
  const PhoneStep({super.key, required this.state});

  final LoginFlowState state;

  @override
  ConsumerState<PhoneStep> createState() => _PhoneStepState();
}

class _PhoneStepState extends ConsumerState<PhoneStep> {
  final _phoneController = TextEditingController();

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Customer sign in', style: Theme.of(context).textTheme.headlineMedium),
        const SizedBox(height: 6),
        Text(
          'We will send you a text with a verification code.',
          style: const TextStyle(color: AppColors.muted),
        ),
        const SizedBox(height: 20),
        TextField(
          controller: _phoneController,
          keyboardType: TextInputType.phone,
          maxLength: 10,
          decoration: const InputDecoration(
            labelText: 'Mobile number',
            prefixText: '+91  ',
            counterText: '',
          ),
        ),
        if (widget.state.error != null) ...[
          const SizedBox(height: 12),
          InlineError(message: widget.state.error!),
        ],
        const SizedBox(height: 16),
        PrimaryButton(
          label: 'Continue',
          isLoading: widget.state.isLoading,
          onPressed: () {
            final phone = _phoneController.text.trim();
            if (phone.length == 10) {
              ref.read(loginFlowControllerProvider.notifier).requestOtp(phone);
            }
          },
        ),
        const SizedBox(height: 12),
        Text(
          'New here? Just enter your number above — we will set up your account for you.',
          style: const TextStyle(color: AppColors.muted, fontSize: 12.5),
        ),
      ],
    );
  }
}
