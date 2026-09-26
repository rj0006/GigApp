import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../domain/entities/address.dart';
import '../../domain/entities/service_item.dart';
import '../../domain/entities/skill_category.dart';
import '../addresses/widgets/address_form_dialog.dart';
import '../providers.dart';

class PostTaskScreen extends ConsumerStatefulWidget {
  const PostTaskScreen({super.key});

  @override
  ConsumerState<PostTaskScreen> createState() => _PostTaskScreenState();
}

class _PostTaskScreenState extends ConsumerState<PostTaskScreen> {
  SkillCategory? _category;
  ServiceItem? _item;
  Address? _address;
  final _descriptionController = TextEditingController();
  final _budgetController = TextEditingController();
  String _urgency = 'normal';
  bool _isSubmitting = false;
  String? _error;

  @override
  void dispose() {
    _descriptionController.dispose();
    _budgetController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final categories = ref.watch(categoriesProvider);
    final addresses = ref.watch(addressesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Post a task')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_error != null) ...[
            Text(_error!, style: const TextStyle(color: Colors.red)),
            const SizedBox(height: 12),
          ],
          Text('What do you need?', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          categories.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => Text('Could not load categories.\n$error'),
            data: (list) => DropdownButtonFormField<SkillCategory>(
              initialValue: _category,
              decoration: const InputDecoration(labelText: 'Category', border: OutlineInputBorder()),
              items: list.map((c) => DropdownMenuItem(value: c, child: Text(c.name))).toList(),
              onChanged: (value) => setState(() {
                _category = value;
                _item = null;
              }),
            ),
          ),
          if (_category != null) ...[
            const SizedBox(height: 16),
            Text('Pick a service', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Consumer(
              builder: (context, ref, _) {
                final items = ref.watch(bookableItemsProvider(_category!.id));
                return items.when(
                  loading: () => const Center(child: CircularProgressIndicator()),
                  error: (error, _) => Text('Could not load services.\n$error'),
                  data: (list) => list.isEmpty
                      ? Text('No services available in this category yet.', style: TextStyle(color: Colors.grey.shade600))
                      : Column(
                          children: list.map((item) => _ServiceItemTile(
                                item: item,
                                selected: _item?.id == item.id,
                                onTap: () => setState(() => _item = item),
                              )).toList(),
                        ),
                );
              },
            ),
          ],
          const SizedBox(height: 16),
          Text('Describe the work', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          TextField(
            controller: _descriptionController,
            maxLines: 3,
            decoration: const InputDecoration(
              hintText: 'What exactly needs to be done?',
              border: OutlineInputBorder(),
            ),
          ),
          if (_item != null && !_item!.allowsInstantBooking) ...[
            const SizedBox(height: 16),
            Text('Your budget', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            TextField(
              controller: _budgetController,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(
                prefixText: '₹ ',
                hintText: 'What are you willing to pay?',
                border: OutlineInputBorder(),
              ),
            ),
          ],
          const SizedBox(height: 16),
          Text('How soon?', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            children: [
              _urgencyChip('urgent', 'Urgent'),
              _urgencyChip('normal', 'Normal'),
              _urgencyChip('flexible', 'Flexible'),
            ],
          ),
          const SizedBox(height: 16),
          Text('Where?', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          addresses.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => Text('Could not load addresses.\n$error'),
            data: (list) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ...list.map((a) => RadioListTile<Address>(
                      value: a,
                      groupValue: _address,
                      onChanged: (value) => setState(() => _address = value),
                      title: Text(a.label),
                      subtitle: Text(a.singleLine),
                      contentPadding: EdgeInsets.zero,
                    )),
                TextButton.icon(
                  onPressed: () async {
                    final saved = await showAddressFormDialog(context);
                    if (saved != null) setState(() => _address = saved);
                  },
                  icon: const Icon(Icons.add),
                  label: const Text('Add a new address'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: _isSubmitting ? null : _submit,
              child: _isSubmitting
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Post task'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _urgencyChip(String value, String label) {
    return ChoiceChip(
      label: Text(label),
      selected: _urgency == value,
      onSelected: (_) => setState(() => _urgency = value),
    );
  }

  Future<void> _submit() async {
    if (_category == null) {
      setState(() => _error = 'Choose a category.');
      return;
    }
    if (_item == null) {
      setState(() => _error = 'Choose a service.');
      return;
    }
    if (_descriptionController.text.trim().length < 5) {
      setState(() => _error = 'Describe the work in a few more words.');
      return;
    }
    if (_address == null) {
      setState(() => _error = 'Choose where the work is needed.');
      return;
    }
    final budget =
        _item!.allowsInstantBooking ? (_item!.basePayout ?? 0) : (double.tryParse(_budgetController.text.trim()) ?? 0);
    if (!_item!.allowsInstantBooking && budget <= 0) {
      setState(() => _error = 'Enter what you are willing to pay.');
      return;
    }

    setState(() {
      _isSubmitting = true;
      _error = null;
    });

    try {
      await ref.read(taskRepositoryProvider).createTask(
            categoryId: _category!.id,
            serviceItemId: _item!.id,
            description: _descriptionController.text.trim(),
            addressId: _address!.id,
            budget: budget,
            urgency: _urgency,
          );
      ref.invalidate(myTasksProvider);
      ref.invalidate(orderHistoryControllerProvider);
      if (mounted) {
        Navigator.of(context).pop(true);
      }
    } on ApiException catch (e) {
      setState(() {
        _isSubmitting = false;
        _error = e.message;
      });
    }
  }
}

class _ServiceItemTile extends StatelessWidget {
  const _ServiceItemTile({required this.item, required this.selected, required this.onTap});

  final ServiceItem item;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      color: selected ? const Color(0xFF4F46E5).withValues(alpha: 0.06) : null,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: BorderSide(color: selected ? const Color(0xFF4F46E5) : Colors.transparent),
      ),
      child: ListTile(
        onTap: onTap,
        leading: Icon(
          selected ? Icons.radio_button_checked : Icons.radio_button_unchecked,
          color: selected ? const Color(0xFF4F46E5) : Colors.grey,
        ),
        title: Text(item.name, style: const TextStyle(fontWeight: FontWeight.w600)),
        subtitle: item.description == null ? null : Text(item.description!),
        trailing: item.allowsInstantBooking && item.basePayout != null
            ? Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text('₹${item.basePayout!.round()}', style: const TextStyle(fontWeight: FontWeight.w700)),
                  const Text('Fixed price', style: TextStyle(fontSize: 10, color: Colors.green)),
                ],
              )
            : const Text('Bidding', style: TextStyle(fontSize: 12, color: Colors.orange)),
      ),
    );
  }
}
