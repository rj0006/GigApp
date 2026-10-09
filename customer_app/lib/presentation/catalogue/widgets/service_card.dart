import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/service_item.dart';
import 'add_to_cart_control.dart';

class ServiceCard extends StatelessWidget {
  const ServiceCard({super.key, required this.item, this.width = 168});

  final ServiceItem item;
  final double width;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.line),
        boxShadow: AppColors.softShadow,
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AspectRatio(
            aspectRatio: 1.3,
            child: item.imageUrl == null
                ? const _Placeholder()
                : Image.network(
                    item.imageUrl!,
                    fit: BoxFit.cover,
                    errorBuilder: (_, _, _) => const _Placeholder(),
                  ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.name,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, height: 1.25, color: AppColors.ink),
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    if (item.basePayout != null)
                      Text(
                        '₹${item.basePayout!.round()}',
                        style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: AppColors.ink),
                      ),
                    const Spacer(),
                    AddToCartControl(serviceItemId: item.id),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Placeholder extends StatelessWidget {
  const _Placeholder();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [AppColors.accentSoft, Color(0xFFF6EFE0)],
        ),
      ),
      child: const Icon(Icons.home_repair_service_outlined, color: AppColors.accent, size: 30),
    );
  }
}
