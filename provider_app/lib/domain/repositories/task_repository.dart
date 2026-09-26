abstract class TaskRepository {
  Future<void> acceptInstantTask(int id);

  Future<void> updateStatus(
    int id, {
    required String status,
    int? stars,
    String? feedback,
    String? cancelReason,
  });
}
