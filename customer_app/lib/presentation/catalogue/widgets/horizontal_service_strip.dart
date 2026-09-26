import 'package:flutter/material.dart';

import '../../../domain/entities/service_item.dart';
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
      padding: const EdgeInsets.only(top: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Row(
              children: [
                Text(title, style: Theme.of(context).textTheme.titleMedium),
                const Spacer(),
                if (onSeeAll != null) TextButton(onPressed: onSeeAll, child: const Text('See all')),
              ],
            ),
          ),
          SizedBox(
            height: 250,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: items.length,
              separatorBuilder: (_, __) => const SizedBox(width: 10),
              itemBuilder: (_, index) => ServiceCard(item: items[index]),
            ),
          ),
        ],
      ),
    );
  }
}
