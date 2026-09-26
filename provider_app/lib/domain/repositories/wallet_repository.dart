import '../entities/ledger_entry.dart';
import '../entities/paged.dart';
import '../entities/wallet_summary.dart';

abstract class WalletRepository {
  Future<WalletSummary> getSummary();
  Future<Paged<LedgerEntry>> getEntries({required int page, int pageSize = 20});
}
