import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/gig_task.dart';
import '../../domain/entities/support_enquiry.dart';
import '../../domain/entities/task_rating.dart';
import '../../domain/repositories/task_repository.dart';

class OrderHistoryState {
  const OrderHistoryState({
    this.orders = const [],
    this.page = 0,
    this.hasNext = true,
    this.isLoading = false,
    this.error,
    this.ratings = const {},
    this.enquiries = const {},
  });

  final List<GigTask> orders;
  final int page;
  final bool hasNext;
  final bool isLoading;
  final Object? error;
  final Map<int, TaskRating> ratings;
  final Map<int, SupportEnquiry> enquiries;

  OrderHistoryState copyWith({
    List<GigTask>? orders,
    int? page,
    bool? hasNext,
    bool? isLoading,
    Object? error,
    Map<int, TaskRating>? ratings,
    Map<int, SupportEnquiry>? enquiries,
  }) {
    return OrderHistoryState(
      orders: orders ?? this.orders,
      page: page ?? this.page,
      hasNext: hasNext ?? this.hasNext,
      isLoading: isLoading ?? this.isLoading,
      error: error,
      ratings: ratings ?? this.ratings,
      enquiries: enquiries ?? this.enquiries,
    );
  }
}

class OrderHistoryController extends StateNotifier<OrderHistoryState> {
  OrderHistoryController(this._repo, {this.status}) : super(const OrderHistoryState()) {
    loadMore();
  }

  final TaskRepository _repo;
  final String? status;

  Future<void> loadMore() async {
    if (state.isLoading || !state.hasNext) return;
    state = state.copyWith(isLoading: true, error: null);
    try {
      final nextPage = state.page + 1;
      final result = await _repo.getOrders(page: nextPage, status: status);
      state = state.copyWith(
        orders: [...state.orders, ...result.orders],
        page: nextPage,
        hasNext: result.hasNext,
        isLoading: false,
        ratings: {...state.ratings, ...result.ratings},
        enquiries: {...state.enquiries, ...result.enquiries},
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e);
    }
  }

  Future<void> refresh() async {
    state = const OrderHistoryState();
    await loadMore();
  }

  void replaceTask(GigTask updated) {
    state = state.copyWith(
      orders: [for (final t in state.orders) t.id == updated.id ? updated : t],
    );
  }

  void putRating(int taskId, TaskRating rating) {
    state = state.copyWith(ratings: {...state.ratings, taskId: rating});
  }

  void putEnquiry(int taskId, SupportEnquiry enquiry) {
    state = state.copyWith(enquiries: {...state.enquiries, taskId: enquiry});
  }
}
