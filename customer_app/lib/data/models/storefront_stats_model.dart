import '../../domain/entities/storefront_stats.dart';

class StorefrontStatsModel extends StorefrontStats {
  const StorefrontStatsModel({
    required super.ratingCount,
    required super.completedCount,
    required super.partnerCount,
    super.averageRating,
  });

  factory StorefrontStatsModel.fromJson(Map<String, dynamic> json) {
    return StorefrontStatsModel(
      averageRating: (json['averageRating'] as num?)?.toDouble(),
      ratingCount: json['ratingCount'] as int? ?? 0,
      completedCount: json['completedCount'] as int? ?? 0,
      partnerCount: json['partnerCount'] as int? ?? 0,
    );
  }
}
