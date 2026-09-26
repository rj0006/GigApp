import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/app_user.dart';
import '../../domain/entities/gig_task.dart';
import '../../domain/entities/storefront_home.dart';
import '../booking/post_task_screen.dart';
import '../catalogue/category_screen.dart';
import '../catalogue/search_screen.dart';
import '../catalogue/widgets/banner_card.dart';
import '../catalogue/widgets/category_tile.dart';
import '../catalogue/widgets/horizontal_service_strip.dart';
import '../providers.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key, required this.user, required this.onSeeOrders});

  final AppUser user;
  final VoidCallback onSeeOrders;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tasks = ref.watch(myTasksProvider);
    final home = ref.watch(storefrontHomeProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(myTasksProvider);
        ref.invalidate(storefrontHomeProvider);
      },
      child: ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
            child: Text('Hello, ${user.name}', style: Theme.of(context).textTheme.headlineSmall),
          ),
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _SearchBar(
              onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const SearchScreen())),
            ),
          ),
          const SizedBox(height: 16),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _ActiveOrdersPreview(tasks: tasks, onSeeOrders: onSeeOrders),
          ),
          home.when(
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: 40),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => Padding(
              padding: const EdgeInsets.all(16),
              child: Text('Could not load services.\n$error', textAlign: TextAlign.center),
            ),
            data: (value) => _StorefrontBody(home: value),
          ),
          const SizedBox(height: 8),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _PostTaskCard(),
          ),
        ],
      ),
    );
  }
}

class _SearchBar extends StatelessWidget {
  const _SearchBar({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        height: 44,
        padding: const EdgeInsets.symmetric(horizontal: 14),
        decoration: BoxDecoration(
          color: Colors.grey.shade100,
          borderRadius: BorderRadius.circular(10),
        ),
        child: Row(
          children: [
            Icon(Icons.search, color: Colors.grey.shade600),
            const SizedBox(width: 10),
            Text('Search for a service', style: TextStyle(color: Colors.grey.shade600)),
          ],
        ),
      ),
    );
  }
}

class _ActiveOrdersPreview extends StatelessWidget {
  const _ActiveOrdersPreview({required this.tasks, required this.onSeeOrders});

  final AsyncValue<List<GigTask>> tasks;
  final VoidCallback onSeeOrders;

  @override
  Widget build(BuildContext context) {
    return tasks.when(
      loading: () => const SizedBox.shrink(),
      error: (_, __) => const SizedBox.shrink(),
      data: (list) {
        final active = list.where((t) => ['pending', 'accepted', 'in_progress'].contains(t.status)).toList();
        if (active.isEmpty) return const SizedBox.shrink();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text('Active orders', style: Theme.of(context).textTheme.titleMedium),
                const Spacer(),
                TextButton(onPressed: onSeeOrders, child: const Text('See all')),
              ],
            ),
            ...active.take(2).map((t) => Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  child: ListTile(
                    title: Text(t.serviceItemName ?? t.categoryName),
                    subtitle: Text(t.statusLabel),
                    trailing: Text('₹${t.effectiveAmount.round()}', style: const TextStyle(fontWeight: FontWeight.w700)),
                  ),
                )),
          ],
        );
      },
    );
  }
}

class _StorefrontBody extends StatelessWidget {
  const _StorefrontBody({required this.home});

  final StorefrontHome home;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (home.spotlight.isNotEmpty) ...[
          const SizedBox(height: 8),
          SizedBox(
            height: 130,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: home.spotlight.length,
              separatorBuilder: (_, __) => const SizedBox(width: 10),
              itemBuilder: (_, index) => BannerCard(banner: home.spotlight[index], width: 260),
            ),
          ),
        ],
        if (home.categories.isNotEmpty) ...[
          const SizedBox(height: 20),
          SizedBox(
            height: 96,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: home.categories.length,
              separatorBuilder: (_, __) => const SizedBox(width: 14),
              itemBuilder: (_, index) {
                final category = home.categories[index];
                return CategoryTile(
                  category: category,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => CategoryScreen(categoryId: category.id, fallbackTitle: category.name)),
                  ),
                );
              },
            ),
          ),
        ],
        HorizontalServiceStrip(title: 'Popular services', items: home.popular),
        HorizontalServiceStrip(title: 'New & noteworthy', items: home.newAndNoteworthy),
        for (final strip in home.strips)
          HorizontalServiceStrip(
            title: strip.categoryName,
            items: strip.services,
            onSeeAll: strip.hasMore
                ? () => Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => CategoryScreen(categoryId: strip.categoryId, fallbackTitle: strip.categoryName),
                      ),
                    )
                : null,
          ),
        if (home.stats.isWorthShowing) ...[
          const SizedBox(height: 20),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _StatsStrip(home: home),
          ),
        ],
      ],
    );
  }
}

class _StatsStrip extends StatelessWidget {
  const _StatsStrip({required this.home});

  final StorefrontHome home;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: Colors.grey.shade100, borderRadius: BorderRadius.circular(12)),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceAround,
        children: [
          if (home.stats.averageRating != null)
            _Stat(value: '${home.stats.averageRating}★', label: '${home.stats.ratingCount} ratings'),
          _Stat(value: '${home.stats.completedCount}+', label: 'jobs done'),
          _Stat(value: '${home.stats.partnerCount}+', label: 'partners'),
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.value, required this.label});

  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(value, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
        Text(label, style: TextStyle(color: Colors.grey.shade600, fontSize: 11)),
      ],
    );
  }
}

class _PostTaskCard extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Card(
      color: const Color(0xFF4F46E5),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PostTaskScreen())),
        child: const Padding(
          padding: EdgeInsets.all(20),
          child: Row(
            children: [
              Icon(Icons.add_circle_outline, color: Colors.white, size: 32),
              SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Need something else?', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 16)),
                    Text('Post a custom task and get bids from partners', style: TextStyle(color: Colors.white70, fontSize: 12)),
                  ],
                ),
              ),
              Icon(Icons.chevron_right, color: Colors.white),
            ],
          ),
        ),
      ),
    );
  }
}
