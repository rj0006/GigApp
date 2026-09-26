class ApiEndpoints {
  ApiEndpoints._();

  static const String otpRequest = '/api/auth/otp/request';
  static const String otpVerify = '/api/auth/otp/verify';
  static const String login = '/api/auth/login';
  static const String registerPartner = '/api/auth/register/partner';
  static const String refresh = '/api/auth/refresh';
  static const String me = '/api/auth/me';
  static const String providerDashboard = '/api/partners/me/dashboard';
  static const String availability = '/api/partners/me/availability';
  static const String myEarnings = '/api/partners/me/earnings';
  static const String myEarningsEntries = '/api/partners/me/earnings/entries';
  static const String profile = '/api/profile';
  static const String profileBank = '/api/profile/bank';
  static const String profilePassword = '/api/profile/password';
  static const String notificationSummary = '/api/notifications/summary';
  static const String notifications = '/api/notifications';
  static const String notificationsRead = '/api/notifications/read';
  static const String submitKyc = '/api/partners/me/kyc';
  static const String serviceArea = '/api/partners/me/service-area';
  static const String devices = '/api/devices';

  static String offerRespond(int id) => '/api/offers/$id/respond';
  static String deviceRevoke(int id) => '/api/devices/$id';

  static String taskAccept(int id) => '/api/gigtasks/$id/accept';
  static String taskStatus(int id) => '/api/gigtasks/$id/status';

  static String bidsForTask(int taskId) => '/api/bids/task/$taskId';
  static String bidWithdraw(int bidId) => '/api/bids/$bidId/withdraw';
  static String bidAcceptCounter(int bidId) => '/api/bids/$bidId/accept-counter';
}
