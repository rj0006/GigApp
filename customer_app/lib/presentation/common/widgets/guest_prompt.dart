import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../auth/open_login.dart';
import 'primary_button.dart';

class GuestPrompt extends StatelessWidget {
  const GuestPrompt({super.key, required this.icon, required this.title, required this.message});

  final IconData icon;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 340),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 96,
                height: 96,
                decoration: const BoxDecoration(color: AppColors.accentSoft, shape: BoxShape.circle),
                child: Icon(icon, size: 44, color: AppColors.accent),
              ),
              const SizedBox(height: 20),
              Text(title, style: Theme.of(context).textTheme.titleLarge, textAlign: TextAlign.center),
              const SizedBox(height: 6),
              Text(message, style: const TextStyle(color: AppColors.muted), textAlign: TextAlign.center),
              const SizedBox(height: 24),
              PrimaryButton(label: 'Sign in or sign up', onPressed: () => openLogin(context)),
            ],
          ),
        ),
      ),
    );
  }
}
