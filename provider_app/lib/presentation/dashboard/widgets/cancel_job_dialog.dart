import 'package:flutter/material.dart';

Future<String?> showCancelJobDialog(
  BuildContext context, {
  required String title,
  required String summary,
}) {
  final reasonController = TextEditingController();

  return showDialog<String>(
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
                controller: reasonController,
                maxLines: 2,
                maxLength: 500,
                autofocus: true,
                decoration: const InputDecoration(labelText: 'Why are you cancelling'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Back')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () => Navigator.of(context).pop(reasonController.text.trim()),
            child: const Text('Yes, cancel this job'),
          ),
        ],
      );
    },
  );
}
