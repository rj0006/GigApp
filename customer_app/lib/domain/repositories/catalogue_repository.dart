import '../entities/service_item.dart';
import '../entities/skill_category.dart';

abstract class CatalogueRepository {
  Future<List<SkillCategory>> getCategories();
  Future<List<ServiceItem>> getBookableItems(int categoryId);
}
