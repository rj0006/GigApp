import 'package:flutter/material.dart';

import '../../../domain/entities/service_item.dart';
import 'add_to_cart_control.dart';

class ServiceCard extends StatelessWidget {
  const ServiceCard({super.key, required this.item, this.width = 160});

  final ServiceItem item;
  final double width;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.grey.shade200),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AspectRatio(
            aspectRatio: 1.3,
            child: item.imageUrl == null
                ? Container(color: Colors.grey.shade100, child: const Icon(Icons.build_outlined, color: Colors.grey))
                : Image.network(
                    item.imageUrl!,
                    fit: BoxFit.cover,
                    errorBuilder: (_, __, ___) =>
                        Container(color: Colors.grey.shade100, child: const Icon(Icons.build_outlined, color: Colors.grey)),
                  ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(10, 8, 10, 10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.name,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                ),
                const SizedBox(height: 6),
                if (item.basePayout != null)
                  Text('₹${item.basePayout!.round()}', style: const TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 8),
                Align(alignment: Alignment.centerLeft, child: AddToCartControl(serviceItemId: item.id)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
