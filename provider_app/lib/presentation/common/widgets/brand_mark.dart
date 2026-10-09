import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';

class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 36, this.light = false});

  final double size;
  final bool light;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: size,
          height: size,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            gradient: AppColors.markGradient,
            borderRadius: BorderRadius.circular(size * 0.3),
            boxShadow: AppColors.accentGlow,
          ),
          child: Text(
            'G',
            style: TextStyle(
              fontFamily: AppTheme.displayFont,
              fontWeight: FontWeight.w800,
              fontSize: size * 0.55,
              color: Colors.white,
              height: 1,
            ),
          ),
        ),
        SizedBox(width: size * 0.3),
        Text.rich(
          TextSpan(
            text: 'Gig',
            children: [
              TextSpan(
                text: 'App',
                style: TextStyle(color: light ? const Color(0xFFA5B4FC) : AppColors.accent),
              ),
            ],
          ),
          style: TextStyle(
            fontFamily: AppTheme.displayFont,
            fontWeight: FontWeight.w800,
            fontSize: size * 0.62,
            letterSpacing: -size * 0.025,
            color: light ? Colors.white : AppColors.ink,
            height: 1,
          ),
        ),
      ],
    );
  }
}
