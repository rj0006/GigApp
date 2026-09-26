import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers.dart';
import 'widgets/service_card.dart';

class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  final _controller = TextEditingController();
  String _term = '';
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () {
      if (mounted) setState(() => _term = value.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _controller,
          autofocus: true,
          textInputAction: TextInputAction.search,
          decoration: const InputDecoration(
            hintText: 'Search for a service',
            border: InputBorder.none,
          ),
          onChanged: _onChanged,
          onSubmitted: (value) => setState(() => _term = value.trim()),
        ),
      ),
      body: _term.isEmpty
          ? Center(child: Text('Search for a service, like "tap repair"', style: TextStyle(color: Colors.grey.shade600)))
          : Consumer(
              builder: (context, ref, _) {
                final results = ref.watch(searchResultsProvider(_term));
                return results.when(
                  loading: () => const Center(child: CircularProgressIndicator()),
                  error: (error, _) => Center(child: Text('Search failed.\n$error', textAlign: TextAlign.center)),
                  data: (items) => items.isEmpty
                      ? Center(child: Text('No services matched "$_term".', style: TextStyle(color: Colors.grey.shade600)))
                      : GridView.builder(
                          padding: const EdgeInsets.all(16),
                          gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                            crossAxisCount: 2,
                            mainAxisSpacing: 12,
                            crossAxisSpacing: 12,
                            childAspectRatio: 0.62,
                          ),
                          itemCount: items.length,
                          itemBuilder: (_, index) => ServiceCard(item: items[index], width: double.infinity),
                        ),
                );
              },
            ),
    );
  }
}
