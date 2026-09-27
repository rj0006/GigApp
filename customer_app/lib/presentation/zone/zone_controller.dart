import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

import '../../core/storage/zone_storage.dart';
import '../../domain/repositories/zone_repository.dart';

class SelectedZone {
  const SelectedZone({required this.id, required this.name});

  final int id;
  final String name;
}

class ZoneController extends StateNotifier<AsyncValue<SelectedZone?>> {
  ZoneController(this._repository, this._storage) : super(const AsyncValue.loading()) {
    _restore();
  }

  final ZoneRepository _repository;
  final ZoneStorage _storage;

  Future<void> _restore() async {
    final saved = await _storage.read();
    state = AsyncValue.data(saved == null ? null : SelectedZone(id: saved.$1, name: saved.$2));
  }

  Future<void> select(int id, String name) async {
    await _storage.save(id, name);
    state = AsyncValue.data(SelectedZone(id: id, name: name));
  }

  Future<void> useCurrentLocation() async {
    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    if (permission == LocationPermission.denied || permission == LocationPermission.deniedForever) {
      throw Exception('Location permission was denied.');
    }

    final serviceEnabled = await Geolocator.isLocationServiceEnabled();
    if (!serviceEnabled) throw Exception('Turn on location services and try again.');

    final position = await Geolocator.getCurrentPosition();
    final nearest = await _repository.getNearest(position.latitude, position.longitude);
    await select(nearest.id, nearest.name);
  }

  Future<void> change() async {
    await _storage.clear();
    state = const AsyncValue.data(null);
  }
}
