import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:provider_app/app.dart';

void main() {
  testWidgets('shows the splash screen on first frame', (WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: ProviderApp()));

    expect(find.text('GigApp'), findsOneWidget);
  });
}
