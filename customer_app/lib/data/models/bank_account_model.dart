import '../../domain/entities/bank_account.dart';

class BankAccountModel extends BankAccount {
  const BankAccountModel({
    required super.id,
    required super.accountHolderName,
    required super.maskedAccountNumber,
    required super.ifscCode,
    required super.bankName,
    super.branchName,
    super.upiId,
  });

  factory BankAccountModel.fromJson(Map<String, dynamic> json) {
    return BankAccountModel(
      id: json['id'] as int,
      accountHolderName: json['accountHolderName'] as String? ?? '',
      maskedAccountNumber: json['maskedAccountNumber'] as String? ?? '',
      ifscCode: json['ifscCode'] as String? ?? '',
      bankName: json['bankName'] as String? ?? '',
      branchName: json['branchName'] as String?,
      upiId: json['upiId'] as String?,
    );
  }
}
