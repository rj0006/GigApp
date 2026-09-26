import 'gig_task.dart';
import 'support_enquiry.dart';
import 'task_rating.dart';

class OrderHistoryPage {
  const OrderHistoryPage({
    required this.orders,
    required this.hasNext,
    required this.ratings,
    required this.enquiries,
  });

  final List<GigTask> orders;
  final bool hasNext;
  final Map<int, TaskRating> ratings;
  final Map<int, SupportEnquiry> enquiries;
}
