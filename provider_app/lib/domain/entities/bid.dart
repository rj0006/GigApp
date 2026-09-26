class Bid {
  const Bid({
    required this.id,
    required this.gigTaskId,
    required this.amount,
    required this.status,
    required this.currentAmount,
    required this.isOpen,
    required this.awaitingPartner,
    this.note,
    this.counterAmount,
    this.counterNote,
    this.taskCategoryName,
    this.taskDescription,
    this.taskBudget,
  });

  final int id;
  final int gigTaskId;
  final double amount;
  final String? note;
  final double? counterAmount;
  final String? counterNote;
  final String status;
  final double currentAmount;
  final bool isOpen;
  final bool awaitingPartner;
  final String? taskCategoryName;
  final String? taskDescription;
  final double? taskBudget;
}
