import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../common/widgets/brand_mark.dart';

class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: AppColors.ink,
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            BrandMark(size: 56, light: true),
            SizedBox(height: 32),
            SizedBox(
              height: 22,
              width: 22,
              child: CircularProgressIndicator(strokeWidth: 2.4, color: Color(0xFFA5B4FC)),
            ),
          ],
        ),
      ),
    );
  }
}
