class Paged<T> {
  const Paged({required this.items, required this.page, required this.hasNext});

  final List<T> items;
  final int page;
  final bool hasNext;
}
