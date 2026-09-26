import '../../core/config/env.dart';
import '../../core/storage/token_storage.dart';
import '../../domain/entities/app_user.dart';
import '../../domain/repositories/auth_repository.dart';
import '../datasources/auth_remote_data_source.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this._remote, this._tokenStorage);

  final AuthRemoteDataSource _remote;
  final TokenStorage _tokenStorage;

  @override
  Future<String?> requestOtp(String phone) => _remote.requestOtp(phone, Env.role);

  @override
  Future<AppUser> verifyOtp(String phone, String code, {String? name}) async {
    final auth = await _remote.verifyOtp(phone, Env.role, code, name);
    await _tokenStorage.save(auth.tokens);
    return auth.user;
  }

  @override
  Future<AppUser> signInWithPassword(String identifier, String password) async {
    final auth = await _remote.login(identifier, password, Env.role);
    await _tokenStorage.save(auth.tokens);
    return auth.user;
  }

  @override
  Future<AppUser?> restoreSession() async {
    final tokens = await _tokenStorage.read();
    if (tokens == null) return null;

    try {
      return await _remote.me();
    } catch (_) {
      await _tokenStorage.clear();
      return null;
    }
  }

  @override
  Future<void> logout() => _tokenStorage.clear();
}
