import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../common/widgets/auth_scaffold.dart';
import '../common/widgets/inline_error.dart';
import '../common/widgets/primary_button.dart';
import '../providers.dart';

class LocationPickerScreen extends ConsumerStatefulWidget {
  const LocationPickerScreen({super.key});

  @override
  ConsumerState<LocationPickerScreen> createState() => _LocationPickerScreenState();
}

class _LocationPickerScreenState extends ConsumerState<LocationPickerScreen> {
  bool _locating = false;
  String? _error;

  Future<void> _useCurrentLocation() async {
    setState(() {
      _locating = true;
      _error = null;
    });
    try {
      await ref.read(zoneControllerProvider.notifier).useCurrentLocation();
    } catch (e) {
      setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _locating = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final zones = ref.watch(zonesProvider);

    return AuthScaffold(
      portalLabel: 'Your area',
      tagline: 'Where do you want your service?',
      points: const ['Detect automatically', 'Or pick from the list'],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Choose your area', style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 6),
          const Text(
            'We only show services in areas we currently cover.',
            style: TextStyle(color: AppColors.muted),
          ),
          const SizedBox(height: 22),
          if (_error != null) ...[
            InlineError(message: _error!),
            const SizedBox(height: 12),
          ],
          PrimaryButton(
            label: 'Use my current location',
            isLoading: _locating,
            onPressed: _useCurrentLocation,
          ),
          const SizedBox(height: 24),
          const Row(
            children: [
              Expanded(child: Divider(color: AppColors.lineStrong)),
              Padding(
                padding: EdgeInsets.symmetric(horizontal: 10),
                child: Text('or choose manually', style: TextStyle(color: AppColors.muted, fontSize: 12.5)),
              ),
              Expanded(child: Divider(color: AppColors.lineStrong)),
            ],
          ),
          const SizedBox(height: 16),
          zones.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => Text('Could not load areas.\n$error', textAlign: TextAlign.center),
            data: (list) => list.isEmpty
                ? const Text('No areas are set up yet.', style: TextStyle(color: AppColors.muted))
                : Column(
                    children: list
                        .map(
                          (zone) => Padding(
                            padding: const EdgeInsets.only(bottom: 10),
                            child: Card(
                              child: ListTile(
                                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: AppColors.accentSoft,
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                  child: const Icon(Icons.place_rounded, color: AppColors.accent, size: 20),
                                ),
                                title: Text(zone.name),
                                trailing: const Icon(Icons.chevron_right_rounded),
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                                onTap: () => ref.read(zoneControllerProvider.notifier).select(zone.id, zone.name),
                              ),
                            ),
                          ),
                        )
                        .toList(),
                  ),
          ),
        ],
      ),
    );
  }
}
