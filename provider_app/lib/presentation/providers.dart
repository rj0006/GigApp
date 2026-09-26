import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../core/network/api_client.dart';
import '../core/realtime/realtime_client.dart';
import '../core/storage/token_storage.dart';
import '../data/datasources/auth_remote_data_source.dart';
import '../data/datasources/bid_remote_data_source.dart';
import '../data/datasources/device_remote_data_source.dart';
import '../data/datasources/notification_remote_data_source.dart';
import '../data/datasources/partner_remote_data_source.dart';
import '../data/datasources/profile_remote_data_source.dart';
import '../data/datasources/skill_category_remote_data_source.dart';
import '../data/datasources/task_remote_data_source.dart';
import '../data/datasources/wallet_remote_data_source.dart';
import '../data/repositories/auth_repository_impl.dart';
import '../data/repositories/bid_repository_impl.dart';
import '../data/repositories/device_repository_impl.dart';
import '../data/repositories/notification_repository_impl.dart';
import '../data/repositories/partner_repository_impl.dart';
import '../data/repositories/profile_repository_impl.dart';
import '../data/repositories/skill_category_repository_impl.dart';
import '../data/repositories/task_repository_impl.dart';
import '../data/repositories/wallet_repository_impl.dart';
import '../domain/entities/app_notification.dart';
import '../domain/entities/device_session.dart';
import '../domain/entities/ledger_entry.dart';
import '../domain/entities/provider_dashboard.dart';
import '../domain/entities/service_area.dart';
import '../domain/entities/skill_category.dart';
import '../domain/entities/wallet_summary.dart';
import '../domain/repositories/auth_repository.dart';
import '../domain/repositories/bid_repository.dart';
import '../domain/repositories/device_repository.dart';
import '../domain/repositories/notification_repository.dart';
import '../domain/repositories/partner_repository.dart';
import '../domain/repositories/profile_repository.dart';
import '../domain/repositories/skill_category_repository.dart';
import '../domain/repositories/task_repository.dart';
import '../domain/repositories/wallet_repository.dart';
import 'auth/session_controller.dart';
import 'common/paged_list_controller.dart';

final tokenStorageProvider = Provider<TokenStorage>((ref) {
  return TokenStorage(const FlutterSecureStorage());
});

final dioProvider = Provider<Dio>((ref) {
  final tokenStorage = ref.watch(tokenStorageProvider);
  return buildApiClient(
    tokenStorage,
    onSessionExpired: () async {
      ref.read(sessionControllerProvider.notifier).forceSignedOut();
    },
  );
});

final authRemoteDataSourceProvider = Provider<AuthRemoteDataSource>((ref) {
  return AuthRemoteDataSource(ref.watch(dioProvider));
});

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepositoryImpl(
    ref.watch(authRemoteDataSourceProvider),
    ref.watch(tokenStorageProvider),
  );
});

final partnerRemoteDataSourceProvider = Provider<PartnerRemoteDataSource>((ref) {
  return PartnerRemoteDataSource(ref.watch(dioProvider));
});

final partnerRepositoryProvider = Provider<PartnerRepository>((ref) {
  return PartnerRepositoryImpl(ref.watch(partnerRemoteDataSourceProvider));
});

final dashboardProvider = FutureProvider.autoDispose<ProviderDashboard>((ref) {
  return ref.watch(partnerRepositoryProvider).getMyDashboard();
});

final skillCategoryRemoteDataSourceProvider = Provider<SkillCategoryRemoteDataSource>((ref) {
  return SkillCategoryRemoteDataSource(ref.watch(dioProvider));
});

final skillCategoryRepositoryProvider = Provider<SkillCategoryRepository>((ref) {
  return SkillCategoryRepositoryImpl(ref.watch(skillCategoryRemoteDataSourceProvider));
});

final skillCategoriesProvider = FutureProvider.autoDispose<List<SkillCategory>>((ref) {
  return ref.watch(skillCategoryRepositoryProvider).getActive();
});

final taskRemoteDataSourceProvider = Provider<TaskRemoteDataSource>((ref) {
  return TaskRemoteDataSource(ref.watch(dioProvider));
});

final taskRepositoryProvider = Provider<TaskRepository>((ref) {
  return TaskRepositoryImpl(ref.watch(taskRemoteDataSourceProvider));
});

final bidRemoteDataSourceProvider = Provider<BidRemoteDataSource>((ref) {
  return BidRemoteDataSource(ref.watch(dioProvider));
});

final bidRepositoryProvider = Provider<BidRepository>((ref) {
  return BidRepositoryImpl(ref.watch(bidRemoteDataSourceProvider));
});

final walletRemoteDataSourceProvider = Provider<WalletRemoteDataSource>((ref) {
  return WalletRemoteDataSource(ref.watch(dioProvider));
});

final walletRepositoryProvider = Provider<WalletRepository>((ref) {
  return WalletRepositoryImpl(ref.watch(walletRemoteDataSourceProvider));
});

final walletSummaryProvider = FutureProvider.autoDispose<WalletSummary>((ref) {
  return ref.watch(walletRepositoryProvider).getSummary();
});

final profileRemoteDataSourceProvider = Provider<ProfileRemoteDataSource>((ref) {
  return ProfileRemoteDataSource(ref.watch(dioProvider));
});

final profileRepositoryProvider = Provider<ProfileRepository>((ref) {
  return ProfileRepositoryImpl(ref.watch(profileRemoteDataSourceProvider));
});

final ledgerControllerProvider =
    StateNotifierProvider.autoDispose<PagedListController<LedgerEntry>, PagedState<LedgerEntry>>((ref) {
  return PagedListController<LedgerEntry>(
    ({required page}) => ref.read(walletRepositoryProvider).getEntries(page: page),
  );
});

final notificationRemoteDataSourceProvider = Provider<NotificationRemoteDataSource>((ref) {
  return NotificationRemoteDataSource(ref.watch(dioProvider));
});

final notificationRepositoryProvider = Provider<NotificationRepository>((ref) {
  return NotificationRepositoryImpl(ref.watch(notificationRemoteDataSourceProvider));
});

final notificationSummaryProvider = FutureProvider.autoDispose<NotificationSummary>((ref) {
  return ref.watch(notificationRepositoryProvider).getSummary();
});

final notificationsControllerProvider = StateNotifierProvider.autoDispose<
    PagedListController<AppNotification>, PagedState<AppNotification>>((ref) {
  return PagedListController<AppNotification>(
    ({required page}) => ref.read(notificationRepositoryProvider).getList(page: page),
  );
});

final notificationToastProvider = StateProvider.autoDispose<String?>((ref) => null);

final realtimeClientProvider = Provider.autoDispose<RealtimeClient>((ref) {
  final client = RealtimeClient(ref.watch(tokenStorageProvider));
  client.onRefresh = (topic) {
    if (topic == 'tasks') {
      ref.invalidate(dashboardProvider);
    }
  };
  client.onNotification = (title, body) {
    ref.invalidate(notificationSummaryProvider);
    ref.read(notificationToastProvider.notifier).state = '$title: $body';
  };
  ref.onDispose(() => client.disconnect());
  return client;
});

final deviceRemoteDataSourceProvider = Provider<DeviceRemoteDataSource>((ref) {
  return DeviceRemoteDataSource(ref.watch(dioProvider), ref.watch(tokenStorageProvider));
});

final deviceRepositoryProvider = Provider<DeviceRepository>((ref) {
  return DeviceRepositoryImpl(ref.watch(deviceRemoteDataSourceProvider));
});

final devicesProvider = FutureProvider.autoDispose<List<DeviceSession>>((ref) {
  return ref.watch(deviceRepositoryProvider).getDevices();
});

final serviceAreaProvider = FutureProvider.autoDispose<ServiceArea>((ref) {
  return ref.watch(partnerRepositoryProvider).getServiceArea();
});
