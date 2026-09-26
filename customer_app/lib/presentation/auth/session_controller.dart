import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/app_user.dart';
import '../providers.dart';

class SessionController extends AsyncNotifier<AppUser?> {
  @override
  Future<AppUser?> build() {
    return ref.watch(authRepositoryProvider).restoreSession();
  }

  void setSignedIn(AppUser user) {
    state = AsyncData(user);
  }

  Future<void> signOut() async {
    await ref.read(authRepositoryProvider).logout();
    state = const AsyncData(null);
  }

  void forceSignedOut() {
    state = const AsyncData(null);
  }
}

final sessionControllerProvider = AsyncNotifierProvider<SessionController, AppUser?>(
  SessionController.new,
);
