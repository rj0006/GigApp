import '../../domain/repositories/task_repository.dart';
import '../datasources/task_remote_data_source.dart';

class TaskRepositoryImpl implements TaskRepository {
  TaskRepositoryImpl(this._remote);

  final TaskRemoteDataSource _remote;

  @override
  Future<void> acceptInstantTask(int id) => _remote.acceptInstantTask(id);

  @override
  Future<void> updateStatus(
    int id, {
    required String status,
    int? stars,
    String? feedback,
    String? cancelReason,
  }) {
    return _remote.updateStatus(
      id,
      status: status,
      stars: stars,
      feedback: feedback,
      cancelReason: cancelReason,
    );
  }
}
