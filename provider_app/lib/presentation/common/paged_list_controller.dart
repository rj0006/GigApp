import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/paged.dart';

class PagedState<T> {
  const PagedState._({
    required this.items,
    required this.page,
    required this.hasNext,
    required this.isLoading,
    this.error,
  });

  // A `const []` default on a generic field types itself as List<Never>, not
  // List<T> — DDC (the web compiler) enforces that strictly at runtime and
  // throws on the first copyWith. This factory builds the empty list inside
  // a call site where T is actually bound, so it comes out as List<T>.
  factory PagedState.initial() =>
      PagedState._(items: <T>[], page: 0, hasNext: true, isLoading: false);

  final List<T> items;
  final int page;
  final bool hasNext;
  final bool isLoading;
  final Object? error;

  PagedState<T> copyWith({
    List<T>? items,
    int? page,
    bool? hasNext,
    bool? isLoading,
    Object? error,
  }) {
    return PagedState._(
      items: items ?? this.items,
      page: page ?? this.page,
      hasNext: hasNext ?? this.hasNext,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

typedef PagedFetcher<T> = Future<Paged<T>> Function({required int page});

class PagedListController<T> extends StateNotifier<PagedState<T>> {
  PagedListController(this._fetch) : super(PagedState<T>.initial()) {
    loadMore();
  }

  final PagedFetcher<T> _fetch;

  Future<void> loadMore() async {
    if (state.isLoading || !state.hasNext) return;
    state = state.copyWith(isLoading: true, error: null);
    try {
      final nextPage = state.page + 1;
      final result = await _fetch(page: nextPage);
      state = state.copyWith(
        items: [...state.items, ...result.items],
        page: nextPage,
        hasNext: result.hasNext,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e);
    }
  }

  Future<void> refresh() async {
    state = PagedState<T>.initial();
    await loadMore();
  }
}
