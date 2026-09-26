import '../../domain/entities/app_user.dart';
import '../../domain/entities/wallet_summary.dart';
import '../../domain/repositories/profile_repository.dart';
import '../datasources/profile_remote_data_source.dart';

class ProfileRepositoryImpl implements ProfileRepository {
  ProfileRepositoryImpl(this._remote);

  final ProfileRemoteDataSource _remote;

  @override
  Future<AppUser> updateProfile({required String name, required String phone, String? email}) {
    return _remote.updateProfile({
      'name': name,
      'phone': phone,
      'email': email,
    });
  }

  @override
  Future<BankAccount?> saveBankAccount({
    required String accountHolderName,
    required String accountNumber,
    required String ifscCode,
    required String bankName,
    String? branchName,
    String? upiId,
  }) {
    return _remote.saveBankAccount({
      'accountHolderName': accountHolderName,
      'accountNumber': accountNumber,
      'ifscCode': ifscCode,
      'bankName': bankName,
      if (branchName != null && branchName.isNotEmpty) 'branchName': branchName,
      if (upiId != null && upiId.isNotEmpty) 'upiId': upiId,
    });
  }

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  }) {
    return _remote.changePassword({
      'currentPassword': currentPassword,
      'newPassword': newPassword,
      'confirmPassword': confirmPassword,
    });
  }
}
