class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.expiresAtUtc,
    required this.refreshToken,
    required this.refreshExpiresAtUtc,
  });

  final String accessToken;
  final DateTime expiresAtUtc;
  final String refreshToken;
  final DateTime refreshExpiresAtUtc;
}
