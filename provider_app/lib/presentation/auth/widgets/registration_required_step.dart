import 'package:flutter/material.dart';

import '../../registration/widgets/partner_registration_form.dart';
import '../login_flow_state.dart';

class RegistrationRequiredStep extends StatelessWidget {
  const RegistrationRequiredStep({super.key, required this.state});

  final LoginFlowState state;

  @override
  Widget build(BuildContext context) {
    return PartnerRegistrationForm(phone: state.phone);
  }
}
