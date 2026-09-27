import '../../domain/entities/service_item.dart';
import '../../domain/entities/skill_category.dart';
import '../../domain/repositories/catalogue_repository.dart';
import '../datasources/catalogue_remote_data_source.dart';

class CatalogueRepositoryImpl implements CatalogueRepository {
  CatalogueRepositoryImpl(this._remote);

  final CatalogueRemoteDataSource _remote;

  @override
  Future<List<SkillCategory>> getCategories() => _remote.getCategories();

  @override
  Future<List<ServiceItem>> getBookableItems(int categoryId, {int? zoneId}) =>
      _remote.getBookableItems(categoryId, zoneId: zoneId);
}
