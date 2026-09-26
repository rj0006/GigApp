import '../../domain/entities/support_enquiry.dart';

class SupportEnquiryModel extends SupportEnquiry {
  const SupportEnquiryModel({
    required super.id,
    required super.gigTaskId,
    required super.topic,
    required super.topicLabel,
    required super.message,
    required super.status,
    required super.statusLabel,
    required super.isLive,
    required super.reference,
    required super.createdAt,
    super.resolution,
  });

  factory SupportEnquiryModel.fromJson(Map<String, dynamic> json) {
    return SupportEnquiryModel(
      id: json['id'] as int,
      gigTaskId: json['gigTaskId'] as int,
      topic: json['topic'] as String? ?? 'other',
      topicLabel: json['topicLabel'] as String? ?? '',
      message: json['message'] as String? ?? '',
      status: json['status'] as String? ?? 'open',
      statusLabel: json['statusLabel'] as String? ?? '',
      isLive: json['isLive'] as bool? ?? true,
      resolution: json['resolution'] as String?,
      reference: json['reference'] as String? ?? '',
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}
