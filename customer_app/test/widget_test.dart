import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:customer_app/app.dart';

void main() {
  testWidgets('shows the splash screen on first frame', (WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: CustomerApp()));

    expect(find.text('GigApp'), findsOneWidget);
  });
}
