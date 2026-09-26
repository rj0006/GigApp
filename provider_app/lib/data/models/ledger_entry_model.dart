import '../../domain/entities/ledger_entry.dart';

class LedgerEntryModel extends LedgerEntry {
  const LedgerEntryModel({
    required super.id,
    required super.entryType,
    required super.typeLabel,
    required super.isCredit,
    required super.amount,
    required super.signedAmount,
    required super.balanceAfter,
    required super.description,
    required super.createdAt,
    super.remark,
    super.taskCategory,
    super.taskAddress,
    super.customerName,
  });

  factory LedgerEntryModel.fromJson(Map<String, dynamic> json) {
    return LedgerEntryModel(
      id: json['id'] as int,
      entryType: json['entryType'] as String? ?? '',
      typeLabel: json['typeLabel'] as String? ?? '',
      isCredit: json['isCredit'] as bool? ?? true,
      amount: (json['amount'] as num? ?? 0).toDouble(),
      signedAmount: (json['signedAmount'] as num? ?? 0).toDouble(),
      balanceAfter: (json['balanceAfter'] as num? ?? 0).toDouble(),
      description: json['description'] as String? ?? '',
      createdAt: DateTime.parse(json['createdAt'] as String),
      remark: json['remark'] as String?,
      taskCategory: json['taskCategory'] as String?,
      taskAddress: json['taskAddress'] as String?,
      customerName: json['customerName'] as String?,
    );
  }
}
