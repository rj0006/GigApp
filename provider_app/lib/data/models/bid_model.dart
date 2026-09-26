import '../../domain/entities/bid.dart';

class BidModel extends Bid {
  const BidModel({
    required super.id,
    required super.gigTaskId,
    required super.amount,
    required super.status,
    required super.currentAmount,
    required super.isOpen,
    required super.awaitingPartner,
    super.note,
    super.counterAmount,
    super.counterNote,
    super.taskCategoryName,
    super.taskDescription,
    super.taskBudget,
  });

  factory BidModel.fromJson(Map<String, dynamic> json) {
    return BidModel(
      id: json['id'] as int,
      gigTaskId: json['gigTaskId'] as int,
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      note: json['note'] as String?,
      counterAmount: (json['counterAmount'] as num?)?.toDouble(),
      counterNote: json['counterNote'] as String?,
      status: json['status'] as String? ?? '',
      currentAmount: (json['currentAmount'] as num?)?.toDouble() ?? 0,
      isOpen: json['isOpen'] as bool? ?? false,
      awaitingPartner: json['awaitingPartner'] as bool? ?? false,
      taskCategoryName: json['taskCategoryName'] as String?,
      taskDescription: json['taskDescription'] as String?,
      taskBudget: (json['taskBudget'] as num?)?.toDouble(),
    );
  }
}
