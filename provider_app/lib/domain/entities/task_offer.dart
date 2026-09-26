import 'gig_task.dart';

class TaskOffer {
  const TaskOffer({
    required this.id,
    required this.gigTaskId,
    required this.expiresAt,
    required this.isLive,
    this.task,
  });

  final int id;
  final int gigTaskId;
  final DateTime expiresAt;
  final bool isLive;
  final GigTask? task;
}
