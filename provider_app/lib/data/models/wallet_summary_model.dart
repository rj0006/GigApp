import '../../domain/entities/wallet_summary.dart';

class WalletSummaryModel extends WalletSummary {
  const WalletSummaryModel({
    required super.balance,
    required super.lifetimeEarned,
    required super.lifetimeCommission,
    required super.lifetimeTax,
    required super.lifetimePaidOut,
    required super.earnedThisMonth,
    required super.jobsPaid,
    required super.commissionPercent,
    required super.lifetimeNet,
    super.bankAccount,
  });

  factory WalletSummaryModel.fromJson(Map<String, dynamic> json) {
    final summary = json['summary'] as Map<String, dynamic>? ?? const {};
    final bank = json['bankAccount'] as Map<String, dynamic>?;

    return WalletSummaryModel(
      balance: (summary['balance'] as num? ?? 0).toDouble(),
      lifetimeEarned: (summary['lifetimeEarned'] as num? ?? 0).toDouble(),
      lifetimeCommission: (summary['lifetimeCommission'] as num? ?? 0).toDouble(),
      lifetimeTax: (summary['lifetimeTax'] as num? ?? 0).toDouble(),
      lifetimePaidOut: (summary['lifetimePaidOut'] as num? ?? 0).toDouble(),
      earnedThisMonth: (summary['earnedThisMonth'] as num? ?? 0).toDouble(),
      jobsPaid: summary['jobsPaid'] as int? ?? 0,
      commissionPercent: (summary['commissionPercent'] as num? ?? 0).toDouble(),
      lifetimeNet: (summary['lifetimeNet'] as num? ?? 0).toDouble(),
      bankAccount: bank == null ? null : BankAccountModel.fromJson(bank),
    );
  }
}

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
