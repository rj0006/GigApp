import 'package:flutter/material.dart';

import '../../../domain/entities/catalog_category.dart';

class CategoryTile extends StatelessWidget {
  const CategoryTile({super.key, required this.category, required this.onTap});

  final CatalogCategory category;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Column(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(32),
            child: SizedBox(
              width: 64,
              height: 64,
              child: category.imageUrl == null
                  ? Container(
                      color: const Color(0xFF4F46E5).withValues(alpha: 0.08),
                      child: const Icon(Icons.home_repair_service_outlined, color: Color(0xFF4F46E5)),
                    )
                  : Image.network(
                      category.imageUrl!,
                      fit: BoxFit.cover,
                      errorBuilder: (_, __, ___) => Container(
                        color: const Color(0xFF4F46E5).withValues(alpha: 0.08),
                        child: const Icon(Icons.home_repair_service_outlined, color: Color(0xFF4F46E5)),
                      ),
                    ),
            ),
          ),
          const SizedBox(height: 6),
          SizedBox(
            width: 72,
            child: Text(
              category.name,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}
