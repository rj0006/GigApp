class AppNotification {
  const AppNotification({
    required this.id,
    required this.type,
    required this.icon,
    required this.title,
    required this.body,
    required this.isRead,
    required this.age,
    this.link,
  });

  final int id;
  final String type;
  final String icon;
  final String title;
  final String body;
  final bool isRead;
  final String age;
  final String? link;
}

class NotificationSummary {
  const NotificationSummary({required this.unreadCount, required this.recent});

  final int unreadCount;
  final List<AppNotification> recent;
}
