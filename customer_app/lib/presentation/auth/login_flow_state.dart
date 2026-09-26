enum LoginStep { phone, code, password }

class LoginFlowState {
  const LoginFlowState({
    this.step = LoginStep.phone,
    this.phone = '',
    this.devCodeHint,
    this.isLoading = false,
    this.error,
  });

  final LoginStep step;
  final String phone;
  final String? devCodeHint;
  final bool isLoading;
  final String? error;

  LoginFlowState copyWith({
    LoginStep? step,
    String? phone,
    String? devCodeHint,
    bool clearDevCodeHint = false,
    bool? isLoading,
    String? error,
    bool clearError = false,
  }) {
    return LoginFlowState(
      step: step ?? this.step,
      phone: phone ?? this.phone,
      devCodeHint: clearDevCodeHint ? null : (devCodeHint ?? this.devCodeHint),
      isLoading: isLoading ?? this.isLoading,
      error: clearError ? null : (error ?? this.error),
    );
  }
}
