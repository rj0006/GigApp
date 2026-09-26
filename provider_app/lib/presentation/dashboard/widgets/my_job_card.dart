import 'package:flutter/material.dart';

import '../../../domain/entities/gig_task.dart';

class MyJobCard extends StatelessWidget {
  const MyJobCard({
    super.key,
    required this.job,
    required this.onStart,
    required this.onComplete,
    required this.onCancel,
  });

  final GigTask job;
  final VoidCallback onStart;
  final VoidCallback onComplete;
  final VoidCallback onCancel;

  Color get _statusColor => switch (job.status) {
        'completed' => Colors.green,
        'cancelled' => Colors.red,
        'in_progress' => Colors.blue,
        'accepted' => Colors.indigo,
        _ => Colors.orange,
      };

  @override
  Widget build(BuildContext context) {
    final canStart = job.status == 'accepted';
    final canComplete = job.status == 'in_progress';
    final canCancel = job.status == 'pending' || job.status == 'accepted' || job.status == 'in_progress';

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text('#${job.id} · ${job.categoryName}', style: const TextStyle(fontWeight: FontWeight.w600)),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: _statusColor.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    job.statusLabel,
                    style: TextStyle(color: _statusColor, fontSize: 11, fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(job.address, style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
            if (job.wasAssignedBySupport) ...[
              const SizedBox(height: 4),
              Text(
                'Given to you by support: ${job.assignmentNote ?? ''}',
                style: TextStyle(color: Colors.grey.shade500, fontSize: 11),
              ),
            ],
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('₹${job.effectiveAmount.round()}', style: const TextStyle(fontWeight: FontWeight.w700)),
                Text(job.customerName ?? '—', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
              ],
            ),
            if (canStart || canComplete || canCancel) ...[
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                children: [
                  if (canStart) FilledButton(onPressed: onStart, child: const Text('Start')),
                  if (canComplete)
                    FilledButton(
                      style: FilledButton.styleFrom(backgroundColor: Colors.green),
                      onPressed: onComplete,
                      child: const Text('Complete'),
                    ),
                  if (canCancel)
                    OutlinedButton(
                      style: OutlinedButton.styleFrom(foregroundColor: Colors.red),
                      onPressed: onCancel,
                      child: const Text('Cancel'),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}
