import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../providers.dart';

class DashboardActionsController extends StateNotifier<AsyncValue<void>> {
  DashboardActionsController(this._ref) : super(const AsyncData(null));

  final Ref _ref;

  Future<bool> run(Future<void> Function() action) async {
    state = const AsyncLoading();
    try {
      await action();
      state = const AsyncData(null);
      _ref.invalidate(dashboardProvider);
      return true;
    } on ApiException catch (e, st) {
      state = AsyncError(e, st);
      return false;
    }
  }

  String? get errorMessage {
    final value = state;
    return value is AsyncError ? (value.error as ApiException).message : null;
  }
}

final dashboardActionsControllerProvider =
    StateNotifierProvider.autoDispose<DashboardActionsController, AsyncValue<void>>((ref) {
  return DashboardActionsController(ref);
});
