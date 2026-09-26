import '../entities/app_user.dart';
import '../entities/bank_account.dart';

abstract class ProfileRepository {
  Future<AppUser> updateProfile({required String name, required String phone, String? email});
  Future<BankAccount?> getBankAccount();
  Future<BankAccount?> saveBankAccount({
    required String accountHolderName,
    required String accountNumber,
    required String ifscCode,
    required String bankName,
    String? branchName,
    String? upiId,
  });
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  });
}
