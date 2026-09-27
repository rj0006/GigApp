class ApiEndpoints {
  ApiEndpoints._();

  static const String otpRequest = '/api/auth/otp/request';
  static const String otpVerify = '/api/auth/otp/verify';
  static const String login = '/api/auth/login';
  static const String refresh = '/api/auth/refresh';
  static const String me = '/api/auth/me';

  static const String myTasks = '/api/gigtasks/my';
  static const String createTask = '/api/gigtasks';
  static const String orders = '/api/profile/orders';

  static const String skillCategories = '/api/skillcategories';
  static const String bookableServiceItems = '/api/serviceitems/bookable';

  static const String storefrontHome = '/api/shop/home';
  static const String storefrontSearch = '/api/shop/search';
  static const String cart = '/api/cart';
  static const String cartItems = '/api/cart/items';
  static const String checkout = '/checkout';
  static const String serviceZones = '/api/servicezones';
  static const String nearestServiceZone = '/api/servicezones/nearest';

  static const String addresses = '/api/addresses';

  static const String profile = '/api/profile';
  static const String profileBank = '/api/profile/bank';
  static const String profilePassword = '/api/profile/password';

  static const String notificationSummary = '/api/notifications/summary';
  static const String notifications = '/api/notifications';
  static const String notificationsRead = '/api/notifications/read';

  static const String devices = '/api/devices';

  static String task(int id) => '/api/gigtasks/$id';
  static String taskStatus(int id) => '/api/gigtasks/$id/status';
  static String taskRate(int id) => '/api/gigtasks/$id/rate';
  static String orderHelp(int id) => '/api/profile/orders/$id/help';
  static String addressDefault(int id) => '/api/addresses/$id/default';
  static String addressItem(int id) => '/api/addresses/$id';
  static String deviceRevoke(int id) => '/api/devices/$id';
  static String storefrontCategory(int id) => '/api/shop/category/$id';
  static String cartItem(int serviceItemId) => '/api/cart/items/$serviceItemId';
}
