import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../domain/entities/app_user.dart';
import '../../domain/entities/provider_dashboard.dart';
import '../auth/session_controller.dart';
import '../common/widgets/confirm_dialog.dart';
import '../providers.dart';
import '../registration/widgets/image_picker_tile.dart';

class KycStatusScreen extends ConsumerWidget {
  const KycStatusScreen({super.key, required this.user, required this.profile});

  final AppUser user;
  final DashboardPartnerProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('KYC status'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
            onPressed: () async {
              final confirmed = await showConfirmDialog(
                context,
                title: 'Sign out?',
                confirmLabel: 'Yes, sign out',
                destructive: true,
              );
              if (confirmed) ref.read(sessionControllerProvider.notifier).signOut();
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(dashboardProvider.future),
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text('Hello, ${user.name}', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 20),
            _StatusCard(profile: profile),
            const SizedBox(height: 20),
            _ResubmitForm(profile: profile),
          ],
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.profile});

  final DashboardPartnerProfile profile;

  @override
  Widget build(BuildContext context) {
    final isRejected = profile.kycStatus == 'rejected';
    final isPending = profile.kycStatus == 'pending';
    final color = isRejected ? Colors.red : (isPending ? Colors.orange : Colors.grey);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            Icon(
              isRejected ? Icons.cancel_outlined : (isPending ? Icons.hourglass_top : Icons.description_outlined),
              color: color,
              size: 36,
            ),
            const SizedBox(height: 10),
            Text(profile.kycLabel, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16, color: color)),
            const SizedBox(height: 8),
            Text(
              isRejected
                  ? 'Your KYC was rejected. Fix the reason below and resubmit.'
                  : isPending
                      ? 'Your documents are under review. An administrator checks your KYC before you can see and bid on work.'
                      : 'Submit your documents below to get started.',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.grey.shade600),
            ),
            if (isRejected && profile.kycRejectionReason != null) ...[
              const SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: Colors.red.shade50, borderRadius: BorderRadius.circular(10)),
                child: Text(
                  profile.kycRejectionReason!,
                  style: TextStyle(color: Colors.red.shade700, fontSize: 13),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ResubmitForm extends ConsumerStatefulWidget {
  const _ResubmitForm({required this.profile});

  final DashboardPartnerProfile profile;

  @override
  ConsumerState<_ResubmitForm> createState() => _ResubmitFormState();
}

class _ResubmitFormState extends ConsumerState<_ResubmitForm> {
  File? _selfie;
  File? _aadhaarFront;
  File? _aadhaarBack;
  final _aadhaarController = TextEditingController();
  bool _isSaving = false;
  String? _error;

  @override
  void dispose() {
    _aadhaarController.dispose();
    super.dispose();
  }

  bool get _hasAnything =>
      _selfie != null || _aadhaarFront != null || _aadhaarBack != null || _aadhaarController.text.trim().isNotEmpty;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              widget.profile.kycStatus == 'rejected' ? 'Resubmit your documents' : 'Update your documents',
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
            ),
            const SizedBox(height: 4),
            Text(
              'Replace only what needs fixing — anything left blank stays as it is.',
              style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
            ),
            const SizedBox(height: 16),
            if (_error != null) ...[
              Text(_error!, style: const TextStyle(color: Colors.red)),
              const SizedBox(height: 12),
            ],
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                ImagePickerTile(
                  label: 'Selfie',
                  file: _selfie,
                  shape: ImageTileShape.circle,
                  onPicked: (f) => setState(() => _selfie = f),
                ),
                ImagePickerTile(
                  label: 'Aadhaar front',
                  file: _aadhaarFront,
                  shape: ImageTileShape.square,
                  onPicked: (f) => setState(() => _aadhaarFront = f),
                ),
                ImagePickerTile(
                  label: 'Aadhaar back',
                  file: _aadhaarBack,
                  shape: ImageTileShape.square,
                  onPicked: (f) => setState(() => _aadhaarBack = f),
                ),
              ],
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _aadhaarController,
              keyboardType: TextInputType.number,
              maxLength: 12,
              decoration: const InputDecoration(labelText: 'Aadhaar number (optional — only if changed)'),
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: (_isSaving || !_hasAnything) ? null : _submit,
                child: _isSaving
                    ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Submit for review'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _submit() async {
    setState(() {
      _isSaving = true;
      _error = null;
    });

    try {
      final aadhaar = _aadhaarController.text.trim();
      await ref.read(partnerRepositoryProvider).submitKyc(
            selfie: _selfie,
            aadhaarFront: _aadhaarFront,
            aadhaarBack: _aadhaarBack,
            aadhaarNumber: aadhaar.isEmpty ? null : aadhaar,
          );
      ref.invalidate(dashboardProvider);
      if (mounted) {
        setState(() {
          _isSaving = false;
          _selfie = null;
          _aadhaarFront = null;
          _aadhaarBack = null;
          _aadhaarController.clear();
        });
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(const SnackBar(
            content: Text('Submitted for review.'),
            backgroundColor: Colors.green,
          ));
      }
    } on ApiException catch (e) {
      setState(() {
        _isSaving = false;
        _error = e.message;
      });
    }
  }
}
