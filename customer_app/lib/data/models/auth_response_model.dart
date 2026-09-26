import '../../domain/entities/auth_tokens.dart';
import 'app_user_model.dart';

class AuthResponseModel {
  const AuthResponseModel({required this.tokens, required this.user});

  final AuthTokens tokens;
  final AppUserModel user;

  factory AuthResponseModel.fromJson(Map<String, dynamic> json) {
    return AuthResponseModel(
      tokens: AuthTokens(
        accessToken: json['token'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshExpiresAtUtc: DateTime.parse(json['refreshExpiresAtUtc'] as String),
      ),
      user: AppUserModel.fromJson(json['user'] as Map<String, dynamic>),
    );
  }
}
