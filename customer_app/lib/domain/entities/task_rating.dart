class TaskRating {
  const TaskRating({
    required this.gigTaskId,
    required this.raterRole,
    required this.stars,
    this.feedback,
  });

  final int gigTaskId;
  final String raterRole;
  final int stars;
  final String? feedback;
}
