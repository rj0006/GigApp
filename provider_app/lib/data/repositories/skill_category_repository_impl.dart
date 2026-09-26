import '../../domain/entities/skill_category.dart';
import '../../domain/repositories/skill_category_repository.dart';
import '../datasources/skill_category_remote_data_source.dart';

class SkillCategoryRepositoryImpl implements SkillCategoryRepository {
  SkillCategoryRepositoryImpl(this._remote);

  final SkillCategoryRemoteDataSource _remote;

  @override
  Future<List<SkillCategory>> getActive() => _remote.getActive();
}
