import 'package:flutter/material.dart';

class RatePartnerResult {
  const RatePartnerResult(this.stars, this.feedback);
  final int stars;
  final String? feedback;
}

Future<RatePartnerResult?> showRatePartnerDialog(BuildContext context, {required String partnerName}) {
  final feedbackController = TextEditingController();
  var stars = 0;

  return showDialog<RatePartnerResult>(
    context: context,
    builder: (context) {
      return StatefulBuilder(
        builder: (context, setState) {
          return AlertDialog(
            title: Text('Rate $partnerName'),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: List.generate(5, (i) {
                    final value = i + 1;
                    return IconButton(
                      padding: EdgeInsets.zero,
                      constraints: const BoxConstraints(),
                      onPressed: () => setState(() => stars = value),
                      icon: Icon(
                        value <= stars ? Icons.star : Icons.star_border,
                        color: Colors.amber,
                        size: 32,
                      ),
                    );
                  }),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: feedbackController,
                  maxLines: 2,
                  maxLength: 500,
                  decoration: const InputDecoration(labelText: 'Anything worth noting (optional)'),
                ),
              ],
            ),
            actions: [
              TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancel')),
              FilledButton(
                onPressed: stars == 0
                    ? null
                    : () => Navigator.of(context).pop(RatePartnerResult(stars, feedbackController.text.trim())),
                child: const Text('Submit rating'),
              ),
            ],
          );
        },
      );
    },
  );
}
