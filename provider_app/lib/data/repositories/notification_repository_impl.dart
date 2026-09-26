import '../../domain/entities/app_notification.dart';
import '../../domain/entities/paged.dart';
import '../../domain/repositories/notification_repository.dart';
import '../datasources/notification_remote_data_source.dart';
import '../models/app_notification_model.dart';

class NotificationRepositoryImpl implements NotificationRepository {
  NotificationRepositoryImpl(this._remote);

  final NotificationRemoteDataSource _remote;

  @override
  Future<NotificationSummary> getSummary() => _remote.getSummary();

  @override
  Future<Paged<AppNotification>> getList({required int page, int pageSize = 20}) async {
    final json = await _remote.getList(page: page, pageSize: pageSize);
    final items = (json['items'] as List<dynamic>? ?? const [])
        .map((e) => AppNotificationModel.fromJson(e as Map<String, dynamic>))
        .toList();
    final totalPages = json['totalPages'] as int? ?? 1;
    return Paged(items: items, page: page, hasNext: page < totalPages);
  }

  @override
  Future<void> markRead({int? notificationId}) => _remote.markRead(notificationId: notificationId);
}
