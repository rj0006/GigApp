class SupportEnquiry {
  const SupportEnquiry({
    required this.id,
    required this.gigTaskId,
    required this.topic,
    required this.topicLabel,
    required this.message,
    required this.status,
    required this.statusLabel,
    required this.isLive,
    required this.reference,
    required this.createdAt,
    this.resolution,
  });

  final int id;
  final int gigTaskId;
  final String topic;
  final String topicLabel;
  final String message;
  final String status;
  final String statusLabel;
  final bool isLive;
  final String? resolution;
  final String reference;
  final DateTime createdAt;
}
