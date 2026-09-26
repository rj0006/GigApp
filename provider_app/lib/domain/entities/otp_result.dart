import 'app_user.dart';

sealed class OtpVerifyResult {
  const OtpVerifyResult();
}

class OtpVerifySignedIn extends OtpVerifyResult {
  const OtpVerifySignedIn(this.user);
  final AppUser user;
}

class OtpVerifyRequiresRegistration extends OtpVerifyResult {
  const OtpVerifyRequiresRegistration();
}
