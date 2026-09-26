import '../../domain/entities/skill_category.dart';

class SkillCategoryModel extends SkillCategory {
  const SkillCategoryModel({required super.id, required super.name});

  factory SkillCategoryModel.fromJson(Map<String, dynamic> json) {
    return SkillCategoryModel(
      id: json['id'] as int,
      name: json['name'] as String? ?? '',
    );
  }
}
