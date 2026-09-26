import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../cart/widgets/cart_button.dart';
import '../providers.dart';
import 'widgets/service_card.dart';

class CategoryScreen extends ConsumerWidget {
  const CategoryScreen({super.key, required this.categoryId, required this.fallbackTitle});

  final int categoryId;
  final String fallbackTitle;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(categoryDetailProvider(categoryId));

    return Scaffold(
      appBar: AppBar(
        title: Text(detail.valueOrNull?.categoryName ?? fallbackTitle),
        actions: const [CartButton()],
      ),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(child: Text('Could not load this category.\n$error', textAlign: TextAlign.center)),
        data: (value) => value.services.isEmpty
            ? Center(child: Text('No services available here yet.', style: TextStyle(color: Colors.grey.shade600)))
            : GridView.builder(
                padding: const EdgeInsets.all(16),
                gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 2,
                  mainAxisSpacing: 12,
                  crossAxisSpacing: 12,
                  childAspectRatio: 0.62,
                ),
                itemCount: value.services.length,
                itemBuilder: (_, index) => ServiceCard(item: value.services[index], width: double.infinity),
              ),
      ),
    );
  }
}
