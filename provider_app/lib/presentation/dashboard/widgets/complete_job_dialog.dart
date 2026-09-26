import 'package:flutter/material.dart';

class CompleteJobResult {
  const CompleteJobResult(this.stars, this.feedback);
  final int stars;
  final String? feedback;
}

Future<CompleteJobResult?> showCompleteJobDialog(
  BuildContext context, {
  required String title,
  required String summary,
  required String customerName,
}) {
  final feedbackController = TextEditingController();
  var stars = 0;

  return showDialog<CompleteJobResult>(
    context: context,
    builder: (context) {
      return StatefulBuilder(
        builder: (context, setState) {
          return AlertDialog(
            title: Text(title),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(summary, style: TextStyle(color: Colors.grey.shade600, fontSize: 13)),
                  const SizedBox(height: 16),
                  Text('How was $customerName?', style: const TextStyle(fontWeight: FontWeight.w600)),
                  const SizedBox(height: 6),
                  Row(
                    children: List.generate(5, (i) {
                      final value = i + 1;
                      return IconButton(
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(),
                        onPressed: () => setState(() => stars = value),
                        icon: Icon(
                          value <= stars ? Icons.star : Icons.star_border,
                          color: Colors.amber,
                          size: 28,
                        ),
                      );
                    }),
                  ),
                  Text(
                    'Your rating is required to close the job. It is not shown to the customer.',
                    style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
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
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: stars == 0
                    ? null
                    : () => Navigator.of(context)
                        .pop(CompleteJobResult(stars, feedbackController.text.trim())),
                child: const Text('Yes, work is done'),
              ),
            ],
          );
        },
      );
    },
  );
}
