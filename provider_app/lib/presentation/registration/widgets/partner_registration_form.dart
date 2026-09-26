import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/entities/skill_category.dart';
import '../../auth/login_flow_controller.dart';
import '../../common/widgets/primary_button.dart';
import '../../providers.dart';
import '../partner_registration_controller.dart';
import 'image_picker_tile.dart';

class PartnerRegistrationForm extends ConsumerStatefulWidget {
  const PartnerRegistrationForm({super.key, required this.phone});

  final String phone;

  @override
  ConsumerState<PartnerRegistrationForm> createState() => _PartnerRegistrationFormState();
}

class _PartnerRegistrationFormState extends ConsumerState<PartnerRegistrationForm> {
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _aadhaarController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _aadhaarController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(partnerRegistrationControllerProvider);
    final controller = ref.read(partnerRegistrationControllerProvider.notifier);
    final categories = ref.watch(skillCategoriesProvider);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Finish your registration', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 6),
        Text(
          '+91 ${widget.phone} is verified. An administrator checks your KYC before you can accept work.',
          style: TextStyle(color: Colors.grey.shade600, fontSize: 13),
        ),
        const SizedBox(height: 16),

        TextField(
          controller: _nameController,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Full name'),
          onChanged: controller.setName,
        ),
        const SizedBox(height: 12),

        TextField(
          controller: _emailController,
          keyboardType: TextInputType.emailAddress,
          decoration: const InputDecoration(labelText: 'Email (optional)'),
          onChanged: controller.setEmail,
        ),
        const SizedBox(height: 12),

        categories.when(
          loading: () => const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: LinearProgressIndicator(),
          ),
          error: (error, _) => Text(
            'Could not load skill categories. Pull to retry.',
            style: TextStyle(color: Colors.red.shade700, fontSize: 13),
          ),
          data: (list) => DropdownButtonFormField<int>(
            initialValue: state.skillCategoryId,
            decoration: const InputDecoration(labelText: 'Skill category'),
            items: list
                .map((SkillCategory c) => DropdownMenuItem(value: c.id, child: Text(c.name)))
                .toList(),
            onChanged: (id) {
              if (id != null) controller.setSkillCategoryId(id);
            },
          ),
        ),
        const SizedBox(height: 16),

        const Text('KYC verification', style: TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        Text(
          'An administrator checks these against each other before approving your account.',
          style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
        ),
        const SizedBox(height: 12),

        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            ImagePickerTile(
              label: 'Your selfie',
              file: state.selfie,
              shape: ImageTileShape.circle,
              onPicked: controller.setSelfie,
            ),
            ImagePickerTile(
              label: 'Aadhaar front',
              file: state.aadhaarFront,
              shape: ImageTileShape.square,
              onPicked: controller.setAadhaarFront,
            ),
            ImagePickerTile(
              label: 'Aadhaar back',
              file: state.aadhaarBack,
              shape: ImageTileShape.square,
              onPicked: controller.setAadhaarBack,
            ),
          ],
        ),
        const SizedBox(height: 16),

        TextField(
          controller: _aadhaarController,
          keyboardType: TextInputType.number,
          maxLength: 12,
          decoration: const InputDecoration(labelText: 'Aadhaar number', counterText: ''),
          onChanged: controller.setAadhaarNumber,
        ),

        if (state.error != null) ...[
          const SizedBox(height: 8),
          Text(state.error!, style: const TextStyle(color: Colors.red)),
        ],
        const SizedBox(height: 16),

        PrimaryButton(
          label: 'Create partner account',
          isLoading: state.isSubmitting,
          onPressed: () => controller.submit(widget.phone),
        ),
        const SizedBox(height: 8),
        Center(
          child: TextButton(
            onPressed: () => ref.read(loginFlowControllerProvider.notifier).useAnotherNumber(),
            child: const Text('Use a different number'),
          ),
        ),
      ],
    );
  }
}
