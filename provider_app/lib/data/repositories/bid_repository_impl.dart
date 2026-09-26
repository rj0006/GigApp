import '../../domain/repositories/bid_repository.dart';
import '../datasources/bid_remote_data_source.dart';

class BidRepositoryImpl implements BidRepository {
  BidRepositoryImpl(this._remote);

  final BidRemoteDataSource _remote;

  @override
  Future<void> placeBid({required int taskId, required double amount, String? note}) {
    return _remote.placeBid(taskId: taskId, amount: amount, note: note);
  }

  @override
  Future<void> withdrawBid(int bidId) => _remote.withdrawBid(bidId);

  @override
  Future<void> acceptCounter(int bidId) => _remote.acceptCounter(bidId);
}
