import 'package:flutter/material.dart';

class AppColors {
  AppColors._();

  static const Color ink = Color(0xFF0B1020);
  static const Color inkSoft = Color(0xFF1A2138);
  static const Color text = Color(0xFF1D2433);
  static const Color muted = Color(0xFF687086);
  static const Color faint = Color(0xFFA2A8BB);
  static const Color line = Color(0xFFE7E9F1);
  static const Color lineStrong = Color(0xFFD8DBE7);
  static const Color canvas = Color(0xFFF5F6FB);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color tint = Color(0xFFF1F2F9);

  static const Color accent = Color(0xFF4F46E5);
  static const Color accentStrong = Color(0xFF4338CA);
  static const Color accentSoft = Color(0xFFEEF0FF);
  static const Color gold = Color(0xFFC9A24A);

  static const Color success = Color(0xFF0F9D58);
  static const Color successSoft = Color(0xFFDEF7EC);
  static const Color successText = Color(0xFF046C4E);
  static const Color warningSoft = Color(0xFFFFF3DC);
  static const Color warningText = Color(0xFF9A5A00);
  static const Color dangerSoft = Color(0xFFFDE8E8);
  static const Color dangerText = Color(0xFFB42318);
  static const Color infoSoft = Color(0xFFE0F2FE);
  static const Color infoText = Color(0xFF075985);

  static const LinearGradient brandGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xFF6366F1), Color(0xFF4F46E5), Color(0xFF4338CA)],
  );

  static const LinearGradient markGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xFF6366F1), Color(0xFF4F46E5), Color(0xFF7C3AED)],
  );

  static List<BoxShadow> get softShadow => [
        BoxShadow(color: const Color(0xFF101828).withValues(alpha: 0.05), blurRadius: 10, offset: const Offset(0, 2)),
        BoxShadow(color: const Color(0xFF101828).withValues(alpha: 0.04), blurRadius: 2, offset: const Offset(0, 1)),
      ];

  static List<BoxShadow> get liftedShadow => [
        BoxShadow(color: const Color(0xFF101828).withValues(alpha: 0.12), blurRadius: 28, offset: const Offset(0, 14), spreadRadius: -10),
        BoxShadow(color: const Color(0xFF101828).withValues(alpha: 0.05), blurRadius: 8, offset: const Offset(0, 3), spreadRadius: -3),
      ];

  static List<BoxShadow> get accentGlow => [
        BoxShadow(color: accent.withValues(alpha: 0.45), blurRadius: 18, offset: const Offset(0, 8), spreadRadius: -6),
      ];
}
