import 'package:flutter/material.dart';

import '../../../domain/entities/gig_task.dart';

class AvailableTaskCard extends StatelessWidget {
  const AvailableTaskCard({super.key, required this.task, required this.onAct});

  final GigTask task;
  final VoidCallback onAct;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      color: task.isUrgent ? const Color(0xFFFDEDED) : null,
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    task.serviceItemName ?? task.categoryName,
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                ),
                if (task.isInstant)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                    decoration: BoxDecoration(
                      color: Colors.green.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: const Text(
                      'Fixed price',
                      style: TextStyle(color: Colors.green, fontSize: 11, fontWeight: FontWeight.w600),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 4),
            Text(task.description, style: TextStyle(color: Colors.grey.shade700)),
            const SizedBox(height: 4),
            Row(
              children: [
                Expanded(
                  child: Text(task.address, style: TextStyle(color: Colors.grey.shade500, fontSize: 12)),
                ),
                if (task.distanceLabel != null)
                  Text(task.distanceLabel!, style: TextStyle(color: Colors.grey.shade500, fontSize: 12)),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('₹${task.effectiveAmount.round()}', style: const TextStyle(fontWeight: FontWeight.w700)),
                    Text(
                      task.isInstant ? 'fixed' : 'budget',
                      style: TextStyle(color: Colors.grey.shade500, fontSize: 11),
                    ),
                  ],
                ),
                FilledButton(
                  style: task.isInstant
                      ? FilledButton.styleFrom(backgroundColor: Colors.green)
                      : null,
                  onPressed: onAct,
                  child: Text(task.isInstant ? 'Accept at ₹${task.effectiveAmount.round()}' : 'Place bid'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
