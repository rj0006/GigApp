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
