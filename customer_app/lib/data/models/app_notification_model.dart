import '../../domain/entities/app_notification.dart';

class AppNotificationModel extends AppNotification {
  const AppNotificationModel({
    required super.id,
    required super.type,
    required super.icon,
    required super.title,
    required super.body,
    required super.isRead,
    required super.age,
    super.link,
  });

  factory AppNotificationModel.fromJson(Map<String, dynamic> json) {
    return AppNotificationModel(
      id: json['id'] as int,
      type: json['type'] as String? ?? 'general',
      icon: json['icon'] as String? ?? '',
      title: json['title'] as String? ?? '',
      body: json['body'] as String? ?? '',
      isRead: json['isRead'] as bool? ?? false,
      age: json['age'] as String? ?? '',
      link: json['link'] as String?,
    );
  }
}

class NotificationSummaryModel extends NotificationSummary {
  const NotificationSummaryModel({required super.unreadCount, required super.recent});

  factory NotificationSummaryModel.fromJson(Map<String, dynamic> json) {
    return NotificationSummaryModel(
      unreadCount: json['unreadCount'] as int? ?? 0,
      recent: ((json['recent'] as List?) ?? [])
          .map((e) => AppNotificationModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}
