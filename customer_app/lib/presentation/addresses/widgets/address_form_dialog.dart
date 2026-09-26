import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../domain/entities/address.dart';
import '../../providers.dart';

Future<Address?> showAddressFormDialog(BuildContext context, {Address? current}) async {
  return showModalBottomSheet<Address>(
    context: context,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(16))),
    builder: (context) => _AddressFormSheet(current: current),
  );
}

class _AddressFormSheet extends ConsumerStatefulWidget {
  const _AddressFormSheet({this.current});

  final Address? current;

  @override
  ConsumerState<_AddressFormSheet> createState() => _AddressFormSheetState();
}

class _AddressFormSheetState extends ConsumerState<_AddressFormSheet> {
  final _formKey = GlobalKey<FormState>();
  late String _label = widget.current?.label ?? 'Home';
  late final _houseController = TextEditingController(text: widget.current?.houseNumber ?? '');
  late final _line1Controller = TextEditingController(text: widget.current?.line1 ?? '');
  late final _line2Controller = TextEditingController(text: widget.current?.line2 ?? '');
  late final _landmarkController = TextEditingController(text: widget.current?.landmark ?? '');
  late final _cityController = TextEditingController(text: widget.current?.city ?? '');
  late final _stateController = TextEditingController(text: widget.current?.state ?? '');
  late final _pincodeController = TextEditingController(text: widget.current?.pincode ?? '');
  bool _isSaving = false;
  String? _error;

  @override
  void dispose() {
    _houseController.dispose();
    _line1Controller.dispose();
    _line2Controller.dispose();
    _landmarkController.dispose();
    _cityController.dispose();
    _stateController.dispose();
    _pincodeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                widget.current == null ? 'Add address' : 'Edit address',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 16),
              if (_error != null) ...[
                Text(_error!, style: const TextStyle(color: Colors.red)),
                const SizedBox(height: 8),
              ],
              Wrap(
                spacing: 8,
                children: ['Home', 'Office', 'Other'].map((label) {
                  return ChoiceChip(
                    label: Text(label),
                    selected: _label == label,
                    onSelected: (_) => setState(() => _label = label),
                  );
                }).toList(),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _houseController,
                decoration: const InputDecoration(labelText: 'Flat / house number (optional)'),
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _line1Controller,
                decoration: const InputDecoration(labelText: 'Address'),
                validator: (v) => (v == null || v.trim().length < 3) ? 'Enter the address' : null,
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _line2Controller,
                decoration: const InputDecoration(labelText: 'Area / street (optional)'),
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _landmarkController,
                decoration: const InputDecoration(labelText: 'Landmark (optional)'),
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _cityController,
                decoration: const InputDecoration(labelText: 'City'),
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter the city' : null,
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _stateController,
                decoration: const InputDecoration(labelText: 'State (optional)'),
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _pincodeController,
                decoration: const InputDecoration(labelText: 'Pincode'),
                keyboardType: TextInputType.number,
                maxLength: 6,
                validator: (v) =>
                    (v == null || !RegExp(r'^[1-9]\d{5}$').hasMatch(v.trim())) ? 'Enter a valid 6-digit pincode' : null,
              ),
              const SizedBox(height: 8),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _isSaving ? null : _submit,
                  child: _isSaving
                      ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('Save address'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isSaving = true;
      _error = null;
    });

    try {
      final saved = await ref.read(addressRepositoryProvider).save(
            id: widget.current?.id,
            label: _label,
            houseNumber: _houseController.text.trim(),
            line1: _line1Controller.text.trim(),
            line2: _line2Controller.text.trim(),
            landmark: _landmarkController.text.trim(),
            city: _cityController.text.trim(),
            state: _stateController.text.trim(),
            pincode: _pincodeController.text.trim(),
            isDefault: widget.current?.isDefault ?? false,
          );
      ref.invalidate(addressesProvider);
      if (mounted) Navigator.of(context).pop(saved);
    } on ApiException catch (e) {
      setState(() {
        _isSaving = false;
        _error = e.message;
      });
    }
  }
}
