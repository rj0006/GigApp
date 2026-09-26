import 'package:signalr_netcore/signalr_client.dart';

import '../config/env.dart';
import '../storage/token_storage.dart';

class RealtimeClient {
  RealtimeClient(this._tokenStorage);

  final TokenStorage _tokenStorage;
  HubConnection? _connection;

  void Function(String topic)? onRefresh;
  void Function(String title, String body)? onNotification;

  Future<void> connect() async {
    if (_connection != null) return;

    final connection = HubConnectionBuilder()
        .withUrl(
          '${Env.apiBaseUrl}/hubs/app',
          options: HttpConnectionOptions(
            accessTokenFactory: () async {
              final tokens = await _tokenStorage.read();
              return tokens?.accessToken ?? '';
            },
          ),
        )
        .withAutomaticReconnect()
        .build();

    connection.on('refresh', (args) {
      final topic = (args != null && args.isNotEmpty) ? args[0] as String? : null;
      if (topic != null) onRefresh?.call(topic);
    });

    connection.on('notification', (args) {
      if (args == null || args.isEmpty) return;
      final note = args[0];
      if (note is Map) {
        onNotification?.call((note['title'] as String?) ?? '', (note['body'] as String?) ?? '');
      }
    });

    _connection = connection;
    try {
      await connection.start();
    } catch (_) {
      // Silent — the app still works from REST calls; the next reconnect
      // attempt (withAutomaticReconnect) or app restart tries again.
    }
  }

  Future<void> disconnect() async {
    await _connection?.stop();
    _connection = null;
  }
}
