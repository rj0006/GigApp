class WalletSummary {
  const WalletSummary({
    required this.balance,
    required this.lifetimeEarned,
    required this.lifetimeCommission,
    required this.lifetimeTax,
    required this.lifetimePaidOut,
    required this.earnedThisMonth,
    required this.jobsPaid,
    required this.commissionPercent,
    required this.lifetimeNet,
    this.bankAccount,
  });

  final double balance;
  final double lifetimeEarned;
  final double lifetimeCommission;
  final double lifetimeTax;
  final double lifetimePaidOut;
  final double earnedThisMonth;
  final int jobsPaid;
  final double commissionPercent;
  final double lifetimeNet;
  final BankAccount? bankAccount;
}

class BankAccount {
  const BankAccount({
    required this.id,
    required this.accountHolderName,
    required this.maskedAccountNumber,
    required this.ifscCode,
    required this.bankName,
    this.branchName,
    this.upiId,
  });

  final int id;
  final String accountHolderName;
  final String maskedAccountNumber;
  final String ifscCode;
  final String bankName;
  final String? branchName;
  final String? upiId;
}
