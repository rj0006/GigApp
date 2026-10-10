enum LoginStep { phone, code, password }

class LoginFlowState {
  const LoginFlowState({
    this.step = LoginStep.phone,
    this.phone = '',
    this.devCodeHint,
    this.isNewAccount = false,
    this.isLoading = false,
    this.error,
  });

  final LoginStep step;
  final String phone;
  final String? devCodeHint;
  final bool isNewAccount;
  final bool isLoading;
  final String? error;

  LoginFlowState copyWith({
    LoginStep? step,
    String? phone,
    String? devCodeHint,
    bool clearDevCodeHint = false,
    bool? isNewAccount,
    bool? isLoading,
    String? error,
    bool clearError = false,
  }) {
    return LoginFlowState(
      step: step ?? this.step,
      phone: phone ?? this.phone,
      devCodeHint: clearDevCodeHint ? null : (devCodeHint ?? this.devCodeHint),
      isNewAccount: isNewAccount ?? this.isNewAccount,
      isLoading: isLoading ?? this.isLoading,
      error: clearError ? null : (error ?? this.error),
    );
  }
}
