class LedgerEntry {
  const LedgerEntry({
    required this.id,
    required this.entryType,
    required this.typeLabel,
    required this.isCredit,
    required this.amount,
    required this.signedAmount,
    required this.balanceAfter,
    required this.description,
    required this.createdAt,
    this.remark,
    this.taskCategory,
    this.taskAddress,
    this.customerName,
  });

  final int id;
  final String entryType;
  final String typeLabel;
  final bool isCredit;
  final double amount;
  final double signedAmount;
  final double balanceAfter;
  final String description;
  final DateTime createdAt;
  final String? remark;
  final String? taskCategory;
  final String? taskAddress;
  final String? customerName;
}
