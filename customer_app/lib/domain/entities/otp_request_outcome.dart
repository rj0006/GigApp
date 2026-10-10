class OtpRequestOutcome {
  const OtpRequestOutcome({this.devCode, this.isNewAccount = false});

  final String? devCode;
  final bool isNewAccount;
}
