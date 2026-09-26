import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../providers.dart';
import 'login_flow_state.dart';
import 'session_controller.dart';

class LoginFlowController extends StateNotifier<LoginFlowState> {
  LoginFlowController(this._ref) : super(const LoginFlowState());

  final Ref _ref;

  Future<void> requestOtp(String phone) async {
    state = state.copyWith(isLoading: true, clearError: true, phone: phone);

    try {
      final devCode = await _ref.read(authRepositoryProvider).requestOtp(phone);
      state = state.copyWith(
        isLoading: false,
        step: LoginStep.code,
        devCodeHint: devCode,
        clearDevCodeHint: devCode == null,
      );
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, error: e.message);
    }
  }

  Future<void> resendOtp() => requestOtp(state.phone);

  Future<void> verifyOtp(String code, {String? name}) async {
    state = state.copyWith(isLoading: true, clearError: true);

    try {
      final user = await _ref.read(authRepositoryProvider).verifyOtp(state.phone, code, name: name);
      state = state.copyWith(isLoading: false);
      _ref.read(sessionControllerProvider.notifier).setSignedIn(user);
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, error: e.message);
    }
  }

  void useAnotherNumber() {
    state = const LoginFlowState();
  }

  void switchToPassword() {
    state = state.copyWith(step: LoginStep.password, clearError: true);
  }

  void useCodeInstead() {
    state = state.copyWith(step: LoginStep.code, clearError: true);
  }

  Future<void> signInWithPassword(String password) async {
    state = state.copyWith(isLoading: true, clearError: true);

    try {
      final user =
          await _ref.read(authRepositoryProvider).signInWithPassword(state.phone, password);
      state = state.copyWith(isLoading: false);
      _ref.read(sessionControllerProvider.notifier).setSignedIn(user);
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, error: e.message);
    }
  }
}

final loginFlowControllerProvider =
    StateNotifierProvider.autoDispose<LoginFlowController, LoginFlowState>((ref) {
  return LoginFlowController(ref);
});
