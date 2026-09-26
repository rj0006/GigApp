import 'package:flutter/material.dart';

import '../../../domain/entities/bid.dart';

class MyBidCard extends StatelessWidget {
  const MyBidCard({
    super.key,
    required this.bid,
    required this.onAcceptCounter,
    required this.onChangeBid,
    required this.onWithdraw,
  });

  final Bid bid;
  final VoidCallback onAcceptCounter;
  final VoidCallback onChangeBid;
  final VoidCallback onWithdraw;

  Color get _statusColor => switch (bid.status) {
        'accepted' => Colors.green,
        'countered' => Colors.orange,
        'rejected' => Colors.red,
        'withdrawn' => Colors.grey,
        _ => Colors.blue,
      };

  @override
  Widget build(BuildContext context) {
    final label = bid.status == 'pending' ? 'Waiting on customer' : bid.status;

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
                  child: Text(
                    '#${bid.gigTaskId} · ${bid.taskCategoryName ?? ''}',
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: _statusColor.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    label,
                    style: TextStyle(color: _statusColor, fontSize: 11, fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
            if (bid.taskDescription != null) ...[
              const SizedBox(height: 4),
              Text(bid.taskDescription!, style: TextStyle(color: Colors.grey.shade700)),
            ],
            const SizedBox(height: 8),
            Row(
              children: [
                Text('Budget ₹${bid.taskBudget?.round() ?? 0}', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                const SizedBox(width: 12),
                Text('Your bid ₹${bid.amount.round()}', style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12)),
              ],
            ),
            if (bid.counterAmount != null) ...[
              const SizedBox(height: 4),
              Text(
                'Countered at ₹${bid.counterAmount!.round()}',
                style: const TextStyle(color: Colors.indigo, fontWeight: FontWeight.w600, fontSize: 12),
              ),
              if (bid.counterNote != null && bid.counterNote!.isNotEmpty)
                Text(
                  bid.counterNote!,
                  style: TextStyle(color: Colors.grey.shade600, fontSize: 12, fontStyle: FontStyle.italic),
                ),
            ],
            if (bid.awaitingPartner || bid.isOpen) ...[
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                children: [
                  if (bid.awaitingPartner)
                    FilledButton(
                      style: FilledButton.styleFrom(backgroundColor: Colors.green),
                      onPressed: onAcceptCounter,
                      child: Text('Accept ₹${bid.currentAmount.round()}'),
                    ),
                  if (bid.isOpen) ...[
                    OutlinedButton(onPressed: onChangeBid, child: const Text('Change bid')),
                    OutlinedButton(
                      style: OutlinedButton.styleFrom(foregroundColor: Colors.red),
                      onPressed: onWithdraw,
                      child: const Text('Withdraw'),
                    ),
                  ],
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}
