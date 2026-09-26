import 'package:flutter/material.dart';

import '../../../domain/entities/catalog_banner.dart';

class BannerCard extends StatelessWidget {
  const BannerCard({super.key, required this.banner, this.width, this.height = 130});

  final CatalogBanner banner;
  final double? width;
  final double height;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width,
      height: height,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(14),
        color: const Color(0xFF4F46E5),
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        fit: StackFit.expand,
        children: [
          if (banner.imageUrl != null)
            Image.network(
              banner.imageUrl!,
              fit: BoxFit.cover,
              errorBuilder: (_, __, ___) => const SizedBox.shrink(),
            ),
          Positioned(
            left: 14,
            right: 14,
            bottom: 12,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  banner.title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                    shadows: [Shadow(blurRadius: 6, color: Colors.black87)],
                  ),
                ),
                if (banner.subtitle != null)
                  Text(
                    banner.subtitle!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white70,
                      fontSize: 11,
                      shadows: [Shadow(blurRadius: 6, color: Colors.black87)],
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
