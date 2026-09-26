class DeviceSession {
  const DeviceSession({
    required this.id,
    required this.deviceLabel,
    required this.createdAt,
    required this.isCurrent,
    this.ipAddress,
    this.lastUsedAt,
  });

  final int id;
  final String deviceLabel;
  final DateTime createdAt;
  final bool isCurrent;
  final String? ipAddress;
  final DateTime? lastUsedAt;
}
