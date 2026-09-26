class CartLine {
  const CartLine({
    required this.serviceItemId,
    required this.serviceItemName,
    required this.categoryId,
    required this.categoryName,
    required this.unitPrice,
    required this.quantity,
    required this.allowsInstantBooking,
    this.imageUrl,
  });

  final int serviceItemId;
  final String serviceItemName;
  final int categoryId;
  final String categoryName;
  final String? imageUrl;
  final double unitPrice;
  final int quantity;
  final bool allowsInstantBooking;

  double get lineTotal => unitPrice * quantity;
}

class Cart {
  const Cart({this.lines = const []});

  final List<CartLine> lines;

  bool get isEmpty => lines.isEmpty;
  int get itemCount => lines.fold(0, (sum, l) => sum + l.quantity);
  double get total => lines.fold(0, (sum, l) => sum + l.lineTotal);

  int quantityOf(int serviceItemId) {
    for (final line in lines) {
      if (line.serviceItemId == serviceItemId) return line.quantity;
    }
    return 0;
  }
}
