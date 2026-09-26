import '../../domain/entities/task_offer.dart';
import 'gig_task_model.dart';

class TaskOfferModel extends TaskOffer {
  const TaskOfferModel({
    required super.id,
    required super.gigTaskId,
    required super.expiresAt,
    required super.isLive,
    super.task,
  });

  factory TaskOfferModel.fromJson(Map<String, dynamic> json) {
    final taskJson = json['task'] as Map<String, dynamic>?;
    return TaskOfferModel(
      id: json['id'] as int,
      gigTaskId: json['gigTaskId'] as int,
      expiresAt: DateTime.parse(json['expiresAt'] as String),
      isLive: json['isLive'] as bool? ?? false,
      task: taskJson == null ? null : GigTaskModel.fromJson(taskJson),
    );
  }
}
