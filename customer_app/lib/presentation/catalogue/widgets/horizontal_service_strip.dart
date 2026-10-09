import 'package:flutter/material.dart';

import '../../../domain/entities/service_item.dart';
import '../../common/widgets/section_header.dart';
import 'service_card.dart';

class HorizontalServiceStrip extends StatelessWidget {
  const HorizontalServiceStrip({super.key, required this.title, required this.items, this.onSeeAll});

  final String title;
  final List<ServiceItem> items;
  final VoidCallback? onSeeAll;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.only(top: 28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: SectionHeader(title: title, onAction: onSeeAll),
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 246,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              clipBehavior: Clip.none,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 2),
              itemCount: items.length,
              separatorBuilder: (_, _) => const SizedBox(width: 12),
              itemBuilder: (_, index) => ServiceCard(item: items[index]),
            ),
          ),
        ],
      ),
    );
  }
}
