import 'package:flutter/material.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';

Future<void> showMoreOptionsSheet(BuildContext context, {required VoidCallback onUsePassword}) {
  return showModalBottomSheet<void>(
    context: context,
    builder: (context) {
      return SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 20),
                child: Text('More options', style: TextStyle(fontFamily: AppTheme.displayFont, fontWeight: FontWeight.w800, fontSize: 20, color: AppColors.ink)),
              ),
              const SizedBox(height: 4),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: Text(
                  'Choose another way to sign in',
                  style: const TextStyle(color: AppColors.muted, fontSize: 13),
                ),
              ),
              const SizedBox(height: 8),
              ListTile(
                leading: const Icon(Icons.lock_outline_rounded, color: AppColors.accent),
                title: const Text('Sign in with password'),
                onTap: () {
                  Navigator.of(context).pop();
                  onUsePassword();
                },
              ),
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 4, 20, 8),
                child: Text(
                  'More ways to receive a code — phone call, WhatsApp, email — are coming soon.',
                  style: TextStyle(color: AppColors.muted, fontSize: 12),
                ),
              ),
            ],
          ),
        ),
      );
    },
  );
}
