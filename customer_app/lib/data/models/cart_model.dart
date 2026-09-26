import '../../core/utils/image_url.dart';
import '../../domain/entities/cart.dart';

class CartLineModel extends CartLine {
  const CartLineModel({
    required super.serviceItemId,
    required super.serviceItemName,
    required super.categoryId,
    required super.categoryName,
    required super.unitPrice,
    required super.quantity,
    required super.allowsInstantBooking,
    super.imageUrl,
  });

  factory CartLineModel.fromJson(Map<String, dynamic> json) {
    return CartLineModel(
      serviceItemId: json['serviceItemId'] as int,
      serviceItemName: json['serviceItemName'] as String? ?? '',
      categoryId: json['categoryId'] as int? ?? 0,
      categoryName: json['categoryName'] as String? ?? '',
      imageUrl: resolveImageUrl(json['imageUrl'] as String?),
      unitPrice: (json['unitPrice'] as num?)?.toDouble() ?? 0,
      quantity: json['quantity'] as int? ?? 0,
      allowsInstantBooking: json['allowsInstantBooking'] as bool? ?? false,
    );
  }
}

class CartModel extends Cart {
  const CartModel({super.lines});

  factory CartModel.fromJson(Map<String, dynamic> json) {
    final lines = json['lines'] as List<dynamic>? ?? const [];
    return CartModel(
      lines: lines.map((e) => CartLineModel.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }
}
