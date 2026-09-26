import '../entities/app_notification.dart';
import '../entities/paged.dart';

abstract class NotificationRepository {
  Future<NotificationSummary> getSummary();
  Future<Paged<AppNotification>> getList({required int page, int pageSize = 20});
  Future<void> markRead({int? notificationId});
}
