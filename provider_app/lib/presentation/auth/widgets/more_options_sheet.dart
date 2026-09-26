import 'package:flutter/material.dart';

Future<void> showMoreOptionsSheet(BuildContext context, {required VoidCallback onUsePassword}) {
  return showModalBottomSheet<void>(
    context: context,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
    ),
    builder: (context) {
      return SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 20),
                child: Text('More options', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 18)),
              ),
              const SizedBox(height: 4),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: Text(
                  'Choose another way to sign in',
                  style: TextStyle(color: Colors.grey.shade600, fontSize: 13),
                ),
              ),
              const SizedBox(height: 8),
              ListTile(
                leading: const Icon(Icons.lock_outline),
                title: const Text('Sign in with password'),
                onTap: () {
                  Navigator.of(context).pop();
                  onUsePassword();
                },
              ),
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 4, 20, 8),
                child: Text(
                  'More ways to receive a code — phone call, WhatsApp, email — are coming soon.',
                  style: TextStyle(color: Colors.grey, fontSize: 12),
                ),
              ),
            ],
          ),
        ),
      );
    },
  );
}
