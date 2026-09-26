import '../entities/skill_category.dart';

abstract class SkillCategoryRepository {
  Future<List<SkillCategory>> getActive();
}
