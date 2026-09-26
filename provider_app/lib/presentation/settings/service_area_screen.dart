import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

import '../../core/network/api_exception.dart';
import '../../domain/entities/service_area.dart';
import '../providers.dart';

class ServiceAreaScreen extends ConsumerWidget {
  const ServiceAreaScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final area = ref.watch(serviceAreaProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Service area')),
      body: area.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(child: Text('Could not load your service area.\n$error', textAlign: TextAlign.center)),
        data: (data) => _ServiceAreaForm(initial: data),
      ),
    );
  }
}

class _ServiceAreaForm extends ConsumerStatefulWidget {
  const _ServiceAreaForm({required this.initial});

  final ServiceArea initial;

  @override
  ConsumerState<_ServiceAreaForm> createState() => _ServiceAreaFormState();
}

class _ServiceAreaFormState extends ConsumerState<_ServiceAreaForm> {
  late double? _lat = widget.initial.baseLatitude;
  late double? _lng = widget.initial.baseLongitude;
  late double _radius = widget.initial.serviceRadiusKm.toDouble();
  late final _cityController = TextEditingController(text: widget.initial.baseCity ?? '');
  late final _pincodeController = TextEditingController(text: widget.initial.basePincode ?? '');
  bool _isLocating = false;
  bool _isSaving = false;
  String? _error;

  @override
  void dispose() {
    _cityController.dispose();
    _pincodeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (widget.initial.openTasksInRange > 0)
          Card(
            color: const Color(0xFFE7F5EC),
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Text('${widget.initial.openTasksInRange} open job(s) in your current range right now.'),
            ),
          ),
        const SizedBox(height: 16),
        Text('Your base location', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 4),
        Text(
          'Work near this point reaches you first. Set it to where you usually start your day.',
          style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(Icons.location_on, color: _lat != null ? const Color(0xFF4F46E5) : Colors.grey),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        _lat != null && _lng != null
                            ? '${_lat!.toStringAsFixed(5)}, ${_lng!.toStringAsFixed(5)}'
                            : 'No location set yet',
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: _isLocating ? null : _useCurrentLocation,
                    icon: _isLocating
                        ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.my_location),
                    label: const Text('Use my current location'),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 20),
        Text('How far will you travel?', style: Theme.of(context).textTheme.titleMedium),
        Text('${_radius.round()} km', style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 18)),
        Slider(
          value: _radius,
          min: 1,
          max: 100,
          divisions: 99,
          label: '${_radius.round()} km',
          onChanged: (v) => setState(() => _radius = v),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _cityController,
          decoration: const InputDecoration(labelText: 'Base city (optional)'),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _pincodeController,
          decoration: const InputDecoration(labelText: 'Base pincode (optional)'),
          keyboardType: TextInputType.number,
        ),
        const SizedBox(height: 20),
        if (_error != null) ...[
          Text(_error!, style: const TextStyle(color: Colors.red)),
          const SizedBox(height: 12),
        ],
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: _isSaving ? null : _save,
            child: _isSaving
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Save'),
          ),
        ),
      ],
    );
  }

  Future<void> _useCurrentLocation() async {
    setState(() {
      _isLocating = true;
      _error = null;
    });
    try {
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied || permission == LocationPermission.deniedForever) {
        setState(() {
          _isLocating = false;
          _error = 'Location permission was denied. Allow it in your phone settings to use this.';
        });
        return;
      }

      if (!await Geolocator.isLocationServiceEnabled()) {
        setState(() {
          _isLocating = false;
          _error = 'Turn on location services on your phone first.';
        });
        return;
      }

      final position = await Geolocator.getCurrentPosition();
      setState(() {
        _lat = position.latitude;
        _lng = position.longitude;
        _isLocating = false;
      });
    } catch (e) {
      setState(() {
        _isLocating = false;
        _error = 'Could not get your location. Try again.';
      });
    }
  }

  Future<void> _save() async {
    setState(() {
      _isSaving = true;
      _error = null;
    });
    try {
      await ref.read(partnerRepositoryProvider).updateServiceArea(
            baseLatitude: _lat,
            baseLongitude: _lng,
            serviceRadiusKm: _radius.round(),
            baseCity: _cityController.text.trim(),
            basePincode: _pincodeController.text.trim(),
          );
      ref.invalidate(serviceAreaProvider);
      ref.invalidate(dashboardProvider);
      if (mounted) {
        setState(() => _isSaving = false);
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(const SnackBar(content: Text('Service area saved.'), backgroundColor: Colors.green));
      }
    } on ApiException catch (e) {
      setState(() {
        _isSaving = false;
        _error = e.message;
      });
    }
  }
}
