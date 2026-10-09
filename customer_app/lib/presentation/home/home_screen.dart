import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../../domain/entities/app_user.dart';
import '../../domain/entities/gig_task.dart';
import '../../domain/entities/storefront_home.dart';
import '../auth/open_login.dart';
import '../booking/post_task_screen.dart';
import '../catalogue/category_screen.dart';
import '../catalogue/search_screen.dart';
import '../catalogue/widgets/banner_card.dart';
import '../catalogue/widgets/category_tile.dart';
import '../catalogue/widgets/horizontal_service_strip.dart';
import '../common/widgets/section_header.dart';
import '../common/widgets/status_chip.dart';
import '../providers.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key, required this.user, required this.onSeeOrders});

  final AppUser? user;
  final VoidCallback onSeeOrders;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tasks = user == null ? const AsyncData<List<GigTask>>([]) : ref.watch(myTasksProvider);
    final home = ref.watch(storefrontHomeProvider);

    return RefreshIndicator(
      onRefresh: () async {
        if (user != null) ref.invalidate(myTasksProvider);
        ref.invalidate(storefrontHomeProvider);
      },
      child: ListView(
        padding: const EdgeInsets.only(bottom: 32),
        children: [
          _Header(
            user: user,
            onSearch: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const SearchScreen())),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 20, 16, 0),
            child: _ActiveOrdersPreview(tasks: tasks, onSeeOrders: onSeeOrders),
          ),
          home.when(
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => Padding(
              padding: const EdgeInsets.all(24),
              child: Text('Could not load services.\n$error', textAlign: TextAlign.center),
            ),
            data: (value) => _StorefrontBody(home: value),
          ),
          const SizedBox(height: 28),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _PostTaskCard(isGuest: user == null),
          ),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.user, required this.onSearch});

  final AppUser? user;
  final VoidCallback onSearch;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 18, 16, 20),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [AppColors.accentSoft, AppColors.canvas],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(user == null ? 'Welcome to GigApp' : 'Hello, ${user!.name}', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 6),
          const _DeliveringToRow(),
          const SizedBox(height: 16),
          _SearchBar(onTap: onSearch),
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
      borderRadius: BorderRadius.circular(16),
      child: Container(
        height: 52,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.line),
          boxShadow: AppColors.softShadow,
        ),
        child: const Row(
          children: [
            Icon(Icons.search_rounded, color: AppColors.accent),
            SizedBox(width: 10),
            Text(
              'Search for a service, like AC repair',
              style: TextStyle(color: AppColors.faint, fontWeight: FontWeight.w500),
            ),
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
      error: (_, _) => const SizedBox.shrink(),
      data: (list) {
        final active = list.where((t) => ['pending', 'accepted', 'in_progress'].contains(t.status)).toList();
        if (active.isEmpty) return const SizedBox.shrink();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SectionHeader(title: 'Active orders', onAction: onSeeOrders),
            const SizedBox(height: 12),
            ...active.take(2).map(
                  (t) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Container(
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: BorderRadius.circular(18),
                        border: Border.all(color: AppColors.line),
                        boxShadow: AppColors.softShadow,
                      ),
                      child: Row(
                        children: [
                          Container(
                            width: 44,
                            height: 44,
                            decoration: BoxDecoration(color: AppColors.accentSoft, borderRadius: BorderRadius.circular(14)),
                            child: const Icon(Icons.handyman_outlined, color: AppColors.accent),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  t.serviceItemName ?? t.categoryName,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14.5, color: AppColors.ink),
                                ),
                                const SizedBox(height: 4),
                                StatusChip(label: t.statusLabel, tone: StatusChip.toneForTask(t.status)),
                              ],
                            ),
                          ),
                          Text(
                            '₹${t.effectiveAmount.round()}',
                            style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: AppColors.ink),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
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
          const SizedBox(height: 24),
          SizedBox(
            height: 150,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: home.spotlight.length,
              separatorBuilder: (_, _) => const SizedBox(width: 12),
              itemBuilder: (_, index) => BannerCard(banner: home.spotlight[index], width: 290),
            ),
          ),
        ],
        if (home.categories.isNotEmpty) ...[
          const SizedBox(height: 28),
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 16),
            child: SectionHeader(title: 'Browse by category'),
          ),
          const SizedBox(height: 14),
          SizedBox(
            height: 114,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: home.categories.length,
              separatorBuilder: (_, _) => const SizedBox(width: 12),
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
          const SizedBox(height: 28),
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
      padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 8),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.line),
        boxShadow: AppColors.softShadow,
      ),
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
        Text(value, style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 2),
        Text(label, style: const TextStyle(color: AppColors.muted, fontSize: 12, fontWeight: FontWeight.w500)),
      ],
    );
  }
}

class _PostTaskCard extends StatelessWidget {
  const _PostTaskCard({required this.isGuest});

  final bool isGuest;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: () => isGuest
          ? openLogin(context)
          : Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PostTaskScreen())),
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          gradient: AppColors.brandGradient,
          borderRadius: BorderRadius.circular(22),
          boxShadow: AppColors.accentGlow,
        ),
        child: const Row(
          children: [
            Icon(Icons.add_circle_outline_rounded, color: Colors.white, size: 34),
            SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Need something else?',
                    style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 16),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'Post a custom task and get bids from partners',
                    style: TextStyle(color: Color(0xCCFFFFFF), fontSize: 12.5),
                  ),
                ],
              ),
            ),
            Icon(Icons.arrow_forward_rounded, color: Colors.white),
          ],
        ),
      ),
    );
  }
}

class _DeliveringToRow extends ConsumerWidget {
  const _DeliveringToRow();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final zoneName = ref.watch(zoneControllerProvider).valueOrNull?.name ?? '';

    return InkWell(
      onTap: () => ref.read(zoneControllerProvider.notifier).change(),
      borderRadius: BorderRadius.circular(999),
      child: Container(
        padding: const EdgeInsets.fromLTRB(10, 6, 8, 6),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: AppColors.line),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.place_rounded, size: 15, color: AppColors.accent),
            const SizedBox(width: 4),
            Text(
              'Delivering to $zoneName',
              style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700, color: AppColors.inkSoft),
            ),
            const SizedBox(width: 2),
            const Icon(Icons.keyboard_arrow_down_rounded, size: 17, color: AppColors.muted),
          ],
        ),
      ),
    );
  }
}
