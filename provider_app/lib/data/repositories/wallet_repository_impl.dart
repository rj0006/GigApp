import '../../domain/entities/ledger_entry.dart';
import '../../domain/entities/paged.dart';
import '../../domain/entities/wallet_summary.dart';
import '../../domain/repositories/wallet_repository.dart';
import '../datasources/wallet_remote_data_source.dart';
import '../models/ledger_entry_model.dart';

class WalletRepositoryImpl implements WalletRepository {
  WalletRepositoryImpl(this._remote);

  final WalletRemoteDataSource _remote;

  @override
  Future<WalletSummary> getSummary() => _remote.getSummary();

  @override
  Future<Paged<LedgerEntry>> getEntries({required int page, int pageSize = 20}) async {
    final json = await _remote.getEntries(page: page, pageSize: pageSize);
    final items = (json['items'] as List<dynamic>? ?? const [])
        .map((e) => LedgerEntryModel.fromJson(e as Map<String, dynamic>))
        .toList();
    final totalPages = json['totalPages'] as int? ?? 1;
    return Paged(items: items, page: page, hasNext: page < totalPages);
  }
}
