import 'package:flutter/material.dart';

class BidDialogResult {
  const BidDialogResult(this.amount, this.note);
  final double amount;
  final String? note;
}

Future<BidDialogResult?> showBidDialog(
  BuildContext context, {
  required String title,
  required String summary,
  double? initialAmount,
  String? initialNote,
  String submitLabel = 'Send bid',
}) {
  final amountController = TextEditingController(
    text: initialAmount == null ? '' : initialAmount.round().toString(),
  );
  final noteController = TextEditingController(text: initialNote ?? '');

  return showDialog<BidDialogResult>(
    context: context,
    builder: (context) {
      return AlertDialog(
        title: Text(title),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(summary, style: TextStyle(color: Colors.grey.shade600, fontSize: 13)),
              const SizedBox(height: 16),
              TextField(
                controller: amountController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Your amount (₹)'),
                autofocus: true,
              ),
              const SizedBox(height: 4),
              Text(
                'You can ask for more or less than the budget.',
                style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: noteController,
                maxLines: 2,
                maxLength: 500,
                decoration: const InputDecoration(labelText: 'Message to the customer (optional)'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              final amount = double.tryParse(amountController.text);
              if (amount == null || amount <= 0) return;
              Navigator.of(context).pop(BidDialogResult(amount, noteController.text.trim()));
            },
            child: Text(submitLabel),
          ),
        ],
      );
    },
  );
}
