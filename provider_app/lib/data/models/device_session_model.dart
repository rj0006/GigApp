import '../../domain/entities/device_session.dart';

class DeviceSessionModel extends DeviceSession {
  const DeviceSessionModel({
    required super.id,
    required super.deviceLabel,
    required super.createdAt,
    required super.isCurrent,
    super.ipAddress,
    super.lastUsedAt,
  });

  factory DeviceSessionModel.fromJson(Map<String, dynamic> json) {
    return DeviceSessionModel(
      id: json['id'] as int,
      deviceLabel: json['deviceLabel'] as String? ?? 'Unknown device',
      createdAt: DateTime.parse(json['createdAt'] as String),
      isCurrent: json['isCurrent'] as bool? ?? false,
      ipAddress: json['ipAddress'] as String?,
      lastUsedAt: json['lastUsedAt'] == null ? null : DateTime.parse(json['lastUsedAt'] as String),
    );
  }
}
