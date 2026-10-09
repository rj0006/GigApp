import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../domain/entities/task_offer.dart';
import '../../providers.dart';
import '../../../core/theme/app_colors.dart';

class OfferBanner extends ConsumerStatefulWidget {
  const OfferBanner({super.key, required this.offer});

  final TaskOffer offer;

  @override
  ConsumerState<OfferBanner> createState() => _OfferBannerState();
}

class _OfferBannerState extends ConsumerState<OfferBanner> {
  late Timer _ticker;
  late int _secondsLeft;
  bool _isResponding = false;

  @override
  void initState() {
    super.initState();
    _secondsLeft = widget.offer.expiresAt.difference(DateTime.now().toUtc()).inSeconds;
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      final left = widget.offer.expiresAt.difference(DateTime.now().toUtc()).inSeconds;
      if (!mounted) return;
      setState(() => _secondsLeft = left);
      if (left <= 0) {
        ref.invalidate(dashboardProvider);
      }
    });
  }

  @override
  void dispose() {
    _ticker.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final task = widget.offer.task;
    if (task == null || _secondsLeft <= 0) return const SizedBox.shrink();

    final minutes = _secondsLeft ~/ 60;
    final seconds = _secondsLeft % 60;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.warningSoft,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: const Color(0xFFF3D9A4)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.bolt, color: AppColors.warningText),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'New job offered to you — ₹${task.effectiveAmount.round()}',
                    style: const TextStyle(fontWeight: FontWeight.w800, color: AppColors.ink),
                  ),
                ),
                Text(
                  '$minutes:${seconds.toString().padLeft(2, '0')}',
                  style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.warningText),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text('${task.serviceItemName ?? task.categoryName} at ${task.address}'),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _isResponding ? null : () => _respond(false),
                    child: const Text('Pass'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: _isResponding ? null : () => _respond(true),
                    child: _isResponding
                        ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('Accept'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _respond(bool accepted) async {
    setState(() => _isResponding = true);
    try {
      await ref.read(partnerRepositoryProvider).respondToOffer(offerId: widget.offer.id, accepted: accepted);
      ref.invalidate(dashboardProvider);
      if (mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(
            content: Text(accepted ? 'Job accepted — check My jobs.' : 'Passed. It will move to the next partner.'),
            backgroundColor: accepted ? AppColors.successText : null,
          ));
      }
    } on ApiException catch (e) {
      if (mounted) {
        setState(() => _isResponding = false);
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(e.message), backgroundColor: AppColors.dangerText));
      }
    }
  }
}
