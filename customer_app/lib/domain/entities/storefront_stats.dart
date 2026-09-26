class StorefrontStats {
  const StorefrontStats({
    required this.ratingCount,
    required this.completedCount,
    required this.partnerCount,
    this.averageRating,
  });

  final double? averageRating;
  final int ratingCount;
  final int completedCount;
  final int partnerCount;

  bool get isWorthShowing => ratingCount >= 5 || completedCount >= 20;
}
