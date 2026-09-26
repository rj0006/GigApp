import 'package:flutter/material.dart';

class RaiseHelpResult {
  const RaiseHelpResult(this.topic, this.message);
  final String topic;
  final String message;
}

const _topics = {
  'payment': 'Payment',
  'quality': 'Quality of work',
  'behaviour': 'Partner behaviour',
  'timing': 'Timing',
  'other': 'Other',
};

Future<RaiseHelpResult?> showRaiseHelpDialog(BuildContext context) {
  final messageController = TextEditingController();
  var topic = 'other';

  return showDialog<RaiseHelpResult>(
    context: context,
    builder: (context) {
      return StatefulBuilder(
        builder: (context, setState) {
          return AlertDialog(
            title: const Text('Get help with this order'),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DropdownButtonFormField<String>(
                  initialValue: topic,
                  decoration: const InputDecoration(labelText: 'What is this about?'),
                  items: _topics.entries.map((e) => DropdownMenuItem(value: e.key, child: Text(e.value))).toList(),
                  onChanged: (value) => setState(() => topic = value ?? 'other'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: messageController,
                  maxLines: 4,
                  maxLength: 1000,
                  decoration: const InputDecoration(labelText: 'Tell us what happened'),
                  onChanged: (_) => setState(() {}),
                ),
              ],
            ),
            actions: [
              TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancel')),
              FilledButton(
                onPressed: messageController.text.trim().length < 10
                    ? null
                    : () => Navigator.of(context).pop(RaiseHelpResult(topic, messageController.text.trim())),
                child: const Text('Send'),
              ),
            ],
          );
        },
      );
    },
  );
}
