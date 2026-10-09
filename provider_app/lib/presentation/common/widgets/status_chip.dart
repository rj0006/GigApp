import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

enum StatusTone { success, warning, danger, info, accent, neutral }

class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.label, this.tone = StatusTone.neutral});

  final String label;
  final StatusTone tone;

  static StatusTone toneForTask(String status) => switch (status) {
        'pending' => StatusTone.warning,
        'accepted' || 'in_progress' => StatusTone.accent,
        'completed' => StatusTone.success,
        'cancelled' => StatusTone.danger,
        _ => StatusTone.neutral,
      };

  @override
  Widget build(BuildContext context) {
    final (background, foreground) = switch (tone) {
      StatusTone.success => (AppColors.successSoft, AppColors.successText),
      StatusTone.warning => (AppColors.warningSoft, AppColors.warningText),
      StatusTone.danger => (AppColors.dangerSoft, AppColors.dangerText),
      StatusTone.info => (AppColors.infoSoft, AppColors.infoText),
      StatusTone.accent => (AppColors.accentSoft, AppColors.accentStrong),
      StatusTone.neutral => (AppColors.tint, AppColors.muted),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: TextStyle(color: foreground, fontSize: 11.5, fontWeight: FontWeight.w700)),
    );
  }
}
