import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../domain/entities/bank_account.dart';
import '../../providers.dart';

Future<bool> showBankAccountDialog(BuildContext context, {BankAccount? current}) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (context) => _BankAccountDialog(current: current),
  );
  return result ?? false;
}

class _BankAccountDialog extends ConsumerStatefulWidget {
  const _BankAccountDialog({this.current});

  final BankAccount? current;

  @override
  ConsumerState<_BankAccountDialog> createState() => _BankAccountDialogState();
}

class _BankAccountDialogState extends ConsumerState<_BankAccountDialog> {
  final _formKey = GlobalKey<FormState>();
  late final _holderCtrl = TextEditingController(text: widget.current?.accountHolderName ?? '');
  final _numberCtrl = TextEditingController();
  late final _ifscCtrl = TextEditingController(text: widget.current?.ifscCode ?? '');
  late final _bankCtrl = TextEditingController(text: widget.current?.bankName ?? '');
  late final _branchCtrl = TextEditingController(text: widget.current?.branchName ?? '');
  late final _upiCtrl = TextEditingController(text: widget.current?.upiId ?? '');
  bool _isSaving = false;
  String? _error;

  @override
  void dispose() {
    _holderCtrl.dispose();
    _numberCtrl.dispose();
    _ifscCtrl.dispose();
    _bankCtrl.dispose();
    _branchCtrl.dispose();
    _upiCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Bank account'),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (_error != null) ...[
                Text(_error!, style: const TextStyle(color: Colors.red)),
                const SizedBox(height: 8),
              ],
              TextFormField(
                controller: _holderCtrl,
                decoration: const InputDecoration(labelText: 'Account holder name'),
                validator: (v) => (v == null || v.trim().length < 2) ? 'Enter the account holder name' : null,
              ),
              TextFormField(
                controller: _numberCtrl,
                decoration: InputDecoration(
                  labelText: 'Account number',
                  hintText: widget.current == null ? null : 'Currently ${widget.current!.maskedAccountNumber}',
                ),
                keyboardType: TextInputType.number,
                validator: (v) => (v == null || v.trim().length < 6) ? 'Enter a valid account number' : null,
              ),
              TextFormField(
                controller: _ifscCtrl,
                decoration: const InputDecoration(labelText: 'IFSC code'),
                textCapitalization: TextCapitalization.characters,
                validator: (v) => (v == null || v.trim().length < 6) ? 'Enter a valid IFSC code' : null,
              ),
              TextFormField(
                controller: _bankCtrl,
                decoration: const InputDecoration(labelText: 'Bank name'),
                validator: (v) => (v == null || v.trim().length < 2) ? 'Enter the bank name' : null,
              ),
              TextFormField(
                controller: _branchCtrl,
                decoration: const InputDecoration(labelText: 'Branch (optional)'),
              ),
              TextFormField(
                controller: _upiCtrl,
                decoration: const InputDecoration(labelText: 'UPI ID (optional)'),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _isSaving ? null : () => Navigator.of(context).pop(false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _isSaving ? null : _submit,
          child: _isSaving
              ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Save'),
        ),
      ],
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isSaving = true;
      _error = null;
    });

    try {
      await ref.read(profileRepositoryProvider).saveBankAccount(
            accountHolderName: _holderCtrl.text.trim(),
            accountNumber: _numberCtrl.text.trim(),
            ifscCode: _ifscCtrl.text.trim(),
            bankName: _bankCtrl.text.trim(),
            branchName: _branchCtrl.text.trim(),
            upiId: _upiCtrl.text.trim(),
          );
      if (mounted) Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      setState(() {
        _isSaving = false;
        _error = e.message;
      });
    }
  }
}
