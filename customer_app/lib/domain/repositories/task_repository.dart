import '../entities/gig_task.dart';
import '../entities/order_history_page.dart';
import '../entities/support_enquiry.dart';

abstract class TaskRepository {
  Future<List<GigTask>> getMyTasks();

  Future<GigTask> createTask({
    required int categoryId,
    required int serviceItemId,
    required String description,
    required int addressId,
    required double budget,
    String urgency,
    DateTime? preferredDateTime,
  });

  Future<GigTask> cancelTask(int id, {String? reason});

  Future<GigTask> rateTask(int id, {required int stars, String? feedback});

  Future<SupportEnquiry> raiseEnquiry(int taskId, {required String topic, required String message});

  Future<OrderHistoryPage> getOrders({required int page, int pageSize = 10, String? status});
}
