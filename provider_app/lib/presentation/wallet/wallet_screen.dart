import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/ledger_entry.dart';
import '../../domain/entities/wallet_summary.dart';
import '../providers.dart';

class WalletScreen extends ConsumerWidget {
  const WalletScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summary = ref.watch(walletSummaryProvider);
    final ledger = ref.watch(ledgerControllerProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(walletSummaryProvider);
        await ref.read(ledgerControllerProvider.notifier).refresh();
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          summary.when(
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: 40),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => Padding(
              padding: const EdgeInsets.symmetric(vertical: 20),
              child: Text('Could not load wallet.\n$error', textAlign: TextAlign.center),
            ),
            data: (data) => _SummaryCard(summary: data),
          ),
          const SizedBox(height: 24),
          Text('Transaction history', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          if (ledger.items.isEmpty && ledger.isLoading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 30),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (ledger.items.isEmpty && ledger.error != null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 20),
              child: Center(
                child: Text('Could not load transactions.\n${ledger.error}', textAlign: TextAlign.center),
              ),
            )
          else if (ledger.items.isEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 20),
              child: Center(
                child: Text('No transactions yet.', style: TextStyle(color: Colors.grey.shade600)),
              ),
            )
          else
            ...ledger.items.map((entry) => _LedgerTile(entry: entry)),
          if (ledger.hasNext)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Center(
                child: ledger.isLoading
                    ? const CircularProgressIndicator()
                    : TextButton(
                        onPressed: () => ref.read(ledgerControllerProvider.notifier).loadMore(),
                        child: const Text('Load more'),
                      ),
              ),
            ),
        ],
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.summary});

  final WalletSummary summary;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: const Color(0xFF4F46E5),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Balance', style: TextStyle(color: Colors.white70, fontSize: 13)),
            const SizedBox(height: 4),
            Text(
              '₹${summary.balance.round()}',
              style: const TextStyle(color: Colors.white, fontSize: 32, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 20),
            Row(
              children: [
                _statChip('This month', '₹${summary.earnedThisMonth.round()}'),
                const SizedBox(width: 10),
                _statChip('Jobs paid', '${summary.jobsPaid}'),
                const SizedBox(width: 10),
                _statChip('Commission', '${summary.commissionPercent.round()}%'),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _statChip(String label, String value) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 8),
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Column(
          children: [
            Text(value, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700)),
            const SizedBox(height: 2),
            Text(label, style: const TextStyle(color: Colors.white70, fontSize: 10)),
          ],
        ),
      ),
    );
  }
}

class _LedgerTile extends StatelessWidget {
  const _LedgerTile({required this.entry});

  final LedgerEntry entry;

  @override
  Widget build(BuildContext context) {
    final color = entry.isCredit ? Colors.green : Colors.red;

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: color.withValues(alpha: 0.12),
          child: Icon(entry.isCredit ? Icons.arrow_downward : Icons.arrow_upward, color: color, size: 18),
        ),
        title: Text(entry.typeLabel, style: const TextStyle(fontWeight: FontWeight.w600)),
        subtitle: Text(
          [
            if (entry.taskCategory != null) entry.taskCategory,
            if (entry.customerName != null) entry.customerName,
            _formatDate(entry.createdAt),
          ].whereType<String>().join(' · '),
          style: TextStyle(fontSize: 12, color: Colors.grey.shade600),
        ),
        trailing: Text(
          '${entry.isCredit ? '+' : '-'}₹${entry.amount.round()}',
          style: TextStyle(color: color, fontWeight: FontWeight.w700),
        ),
      ),
    );
  }

  String _formatDate(DateTime dt) => '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')}/${dt.year}';
}
