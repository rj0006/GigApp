import '../../domain/entities/gig_task.dart';
import '../../domain/entities/order_history_page.dart';
import '../../domain/entities/support_enquiry.dart';
import '../../domain/entities/task_rating.dart';
import '../../domain/repositories/task_repository.dart';
import '../datasources/task_remote_data_source.dart';
import '../models/gig_task_model.dart';
import '../models/support_enquiry_model.dart';

class TaskRepositoryImpl implements TaskRepository {
  TaskRepositoryImpl(this._remote);

  final TaskRemoteDataSource _remote;

  @override
  Future<List<GigTask>> getMyTasks() => _remote.getMyTasks();

  @override
  Future<GigTask> createTask({
    required int categoryId,
    required int serviceItemId,
    required String description,
    required int addressId,
    required double budget,
    String urgency = 'normal',
    DateTime? preferredDateTime,
  }) {
    return _remote.createTask({
      'categoryId': categoryId,
      'serviceItemId': serviceItemId,
      'description': description,
      'addressId': addressId,
      'budget': budget,
      'urgency': urgency,
      if (preferredDateTime != null) 'preferredDateTime': preferredDateTime.toUtc().toIso8601String(),
    });
  }

  @override
  Future<GigTask> cancelTask(int id, {String? reason}) {
    return _remote.updateStatus(id, {
      'status': 'cancelled',
      'stars': 0,
      if (reason != null && reason.isNotEmpty) 'cancelReason': reason,
    });
  }

  @override
  Future<GigTask> rateTask(int id, {required int stars, String? feedback}) {
    return _remote.rate(id, {
      'stars': stars,
      if (feedback != null && feedback.isNotEmpty) 'feedback': feedback,
    });
  }

  @override
  Future<SupportEnquiry> raiseEnquiry(int taskId, {required String topic, required String message}) async {
    final json = await _remote.raiseHelp(taskId, {'topic': topic, 'message': message});
    return SupportEnquiryModel.fromJson(json);
  }

  @override
  Future<OrderHistoryPage> getOrders({required int page, int pageSize = 10, String? status}) async {
    final json = await _remote.getOrders(page: page, pageSize: pageSize, status: status);
    final ordersJson = json['orders'] as Map<String, dynamic>? ?? const {};
    final items = (ordersJson['items'] as List<dynamic>? ?? const [])
        .map((e) => GigTaskModel.fromJson(e as Map<String, dynamic>))
        .toList();
    final hasNext = ordersJson['hasNext'] as bool? ?? false;

    final ratingsJson = json['ratings'] as Map<String, dynamic>? ?? const {};
    final ratings = ratingsJson.map((key, value) {
      final r = value as Map<String, dynamic>;
      return MapEntry(
        int.parse(key),
        TaskRating(
          gigTaskId: r['gigTaskId'] as int,
          raterRole: r['raterRole'] as String? ?? '',
          stars: r['stars'] as int? ?? 0,
          feedback: r['feedback'] as String?,
        ),
      );
    });

    final enquiriesJson = json['enquiries'] as Map<String, dynamic>? ?? const {};
    final enquiries = enquiriesJson.map(
      (key, value) => MapEntry(int.parse(key), SupportEnquiryModel.fromJson(value as Map<String, dynamic>)),
    );

    return OrderHistoryPage(orders: items, hasNext: hasNext, ratings: ratings, enquiries: enquiries);
  }
}
