import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../core/network/api_client.dart';
import '../core/realtime/realtime_client.dart';
import '../core/storage/token_storage.dart';
import '../core/storage/zone_storage.dart';
import '../data/datasources/address_remote_data_source.dart';
import '../data/datasources/auth_remote_data_source.dart';
import '../data/datasources/cart_remote_data_source.dart';
import '../data/datasources/catalogue_remote_data_source.dart';
import '../data/datasources/device_remote_data_source.dart';
import '../data/datasources/notification_remote_data_source.dart';
import '../data/datasources/profile_remote_data_source.dart';
import '../data/datasources/storefront_remote_data_source.dart';
import '../data/datasources/task_remote_data_source.dart';
import '../data/datasources/zone_remote_data_source.dart';
import '../data/repositories/address_repository_impl.dart';
import '../data/repositories/auth_repository_impl.dart';
import '../data/repositories/cart_repository_impl.dart';
import '../data/repositories/catalogue_repository_impl.dart';
import '../data/repositories/device_repository_impl.dart';
import '../data/repositories/notification_repository_impl.dart';
import '../data/repositories/profile_repository_impl.dart';
import '../data/repositories/storefront_repository_impl.dart';
import '../data/repositories/task_repository_impl.dart';
import '../data/repositories/zone_repository_impl.dart';
import '../domain/entities/address.dart';
import '../domain/entities/app_notification.dart';
import '../domain/entities/cart.dart';
import '../domain/entities/category_detail.dart';
import '../domain/entities/device_session.dart';
import '../domain/entities/gig_task.dart';
import '../domain/entities/service_item.dart';
import '../domain/entities/service_zone.dart';
import '../domain/entities/skill_category.dart';
import '../domain/entities/storefront_home.dart';
import '../domain/repositories/address_repository.dart';
import '../domain/repositories/auth_repository.dart';
import '../domain/repositories/cart_repository.dart';
import '../domain/repositories/catalogue_repository.dart';
import '../domain/repositories/device_repository.dart';
import '../domain/repositories/notification_repository.dart';
import '../domain/repositories/profile_repository.dart';
import '../domain/repositories/storefront_repository.dart';
import '../domain/repositories/task_repository.dart';
import '../domain/repositories/zone_repository.dart';
import 'auth/session_controller.dart';
import 'cart/cart_controller.dart';
import 'common/paged_list_controller.dart';
import 'orders/order_history_controller.dart';
import 'zone/zone_controller.dart';

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

final taskRemoteDataSourceProvider = Provider<TaskRemoteDataSource>((ref) {
  return TaskRemoteDataSource(ref.watch(dioProvider));
});

final taskRepositoryProvider = Provider<TaskRepository>((ref) {
  return TaskRepositoryImpl(ref.watch(taskRemoteDataSourceProvider));
});

final myTasksProvider = FutureProvider.autoDispose<List<GigTask>>((ref) {
  return ref.watch(taskRepositoryProvider).getMyTasks();
});

final orderHistoryControllerProvider =
    StateNotifierProvider.autoDispose<OrderHistoryController, OrderHistoryState>((ref) {
  return OrderHistoryController(ref.watch(taskRepositoryProvider));
});

final catalogueRemoteDataSourceProvider = Provider<CatalogueRemoteDataSource>((ref) {
  return CatalogueRemoteDataSource(ref.watch(dioProvider));
});

final catalogueRepositoryProvider = Provider<CatalogueRepository>((ref) {
  return CatalogueRepositoryImpl(ref.watch(catalogueRemoteDataSourceProvider));
});

final categoriesProvider = FutureProvider.autoDispose<List<SkillCategory>>((ref) {
  return ref.watch(catalogueRepositoryProvider).getCategories();
});

final bookableItemsProvider = FutureProvider.autoDispose.family((ref, int categoryId) {
  final zoneId = ref.watch(zoneControllerProvider).valueOrNull?.id;
  return ref.watch(catalogueRepositoryProvider).getBookableItems(categoryId, zoneId: zoneId);
});

final addressRemoteDataSourceProvider = Provider<AddressRemoteDataSource>((ref) {
  return AddressRemoteDataSource(ref.watch(dioProvider));
});

final addressRepositoryProvider = Provider<AddressRepository>((ref) {
  return AddressRepositoryImpl(ref.watch(addressRemoteDataSourceProvider));
});

final addressesProvider = FutureProvider.autoDispose<List<Address>>((ref) {
  return ref.watch(addressRepositoryProvider).getAddresses();
});

final profileRemoteDataSourceProvider = Provider<ProfileRemoteDataSource>((ref) {
  return ProfileRemoteDataSource(ref.watch(dioProvider));
});

final profileRepositoryProvider = Provider<ProfileRepository>((ref) {
  return ProfileRepositoryImpl(ref.watch(profileRemoteDataSourceProvider));
});

final bankAccountProvider = FutureProvider.autoDispose((ref) {
  return ref.watch(profileRepositoryProvider).getBankAccount();
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
      ref.invalidate(myTasksProvider);
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

final storefrontRemoteDataSourceProvider = Provider<StorefrontRemoteDataSource>((ref) {
  return StorefrontRemoteDataSource(ref.watch(dioProvider));
});

final storefrontRepositoryProvider = Provider<StorefrontRepository>((ref) {
  return StorefrontRepositoryImpl(ref.watch(storefrontRemoteDataSourceProvider));
});

int _requireZoneId(Ref ref) {
  final zoneId = ref.watch(zoneControllerProvider).valueOrNull?.id;
  if (zoneId == null) throw StateError('No service zone selected yet.');
  return zoneId;
}

final storefrontHomeProvider = FutureProvider.autoDispose<StorefrontHome>((ref) {
  return ref.watch(storefrontRepositoryProvider).getHome(_requireZoneId(ref));
});

final categoryDetailProvider = FutureProvider.autoDispose.family<CategoryDetail, int>((ref, categoryId) {
  return ref.watch(storefrontRepositoryProvider).getCategory(categoryId, _requireZoneId(ref));
});

final searchResultsProvider = FutureProvider.autoDispose.family<List<ServiceItem>, String>((ref, term) {
  return ref.watch(storefrontRepositoryProvider).search(term, _requireZoneId(ref));
});

final cartRemoteDataSourceProvider = Provider<CartRemoteDataSource>((ref) {
  return CartRemoteDataSource(ref.watch(dioProvider));
});

final cartRepositoryProvider = Provider<CartRepository>((ref) {
  return CartRepositoryImpl(ref.watch(cartRemoteDataSourceProvider));
});

final cartControllerProvider = StateNotifierProvider.autoDispose<CartController, AsyncValue<Cart>>((ref) {
  return CartController(ref.watch(cartRepositoryProvider));
});

final zoneRemoteDataSourceProvider = Provider<ZoneRemoteDataSource>((ref) {
  return ZoneRemoteDataSource(ref.watch(dioProvider));
});

final zoneRepositoryProvider = Provider<ZoneRepository>((ref) {
  return ZoneRepositoryImpl(ref.watch(zoneRemoteDataSourceProvider));
});

final zoneStorageProvider = Provider<ZoneStorage>((ref) {
  return ZoneStorage(const FlutterSecureStorage());
});

final zoneControllerProvider = StateNotifierProvider<ZoneController, AsyncValue<SelectedZone?>>((ref) {
  return ZoneController(ref.watch(zoneRepositoryProvider), ref.watch(zoneStorageProvider));
});

final zonesProvider = FutureProvider.autoDispose<List<ServiceZoneOption>>((ref) {
  return ref.watch(zoneRepositoryProvider).getZones();
});
