abstract class BidRepository {
  Future<void> placeBid({required int taskId, required double amount, String? note});

  Future<void> withdrawBid(int bidId);

  Future<void> acceptCounter(int bidId);
}
