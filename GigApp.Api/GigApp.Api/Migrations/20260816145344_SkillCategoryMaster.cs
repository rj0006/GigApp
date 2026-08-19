using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <summary>
    /// Replaces the free-text Partner.SkillCategory and GigTask.Category columns
    /// with foreign keys into a new admin-managed master.
    ///
    /// The scaffolded version dropped the text columns before the master existed,
    /// which would have discarded every existing value and left the new FKs
    /// pointing at id 0. The order here is: create master → seed it from the
    /// values already in use → add nullable FKs → backfill → enforce NOT NULL →
    /// drop the old text columns.
    /// </summary>
    public partial class SkillCategoryMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --------------------------------------------------- 1. master table
            migrationBuilder.CreateTable(
                name: "SkillCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillCategories_Name",
                table: "SkillCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SkillCategories_IsActive_DisplayOrder",
                table: "SkillCategories",
                columns: new[] { "IsActive", "DisplayOrder" });

            // "Plumbing" and "plumbing" must not coexist. EF cannot express a
            // functional index, so it is created directly.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""UX_SkillCategories_Name_Lower""
                  ON ""SkillCategories"" (LOWER(""Name""));");

            // ------------------------------------- 2. seed from values in use
            migrationBuilder.Sql(@"
                INSERT INTO ""SkillCategories"" (""Name"", ""IsActive"", ""DisplayOrder"", ""CreatedAt"")
                SELECT DISTINCT ON (LOWER(name)) name, TRUE, 0, NOW()
                FROM (
                    SELECT TRIM(""SkillCategory"") AS name
                      FROM ""Partners""
                     WHERE TRIM(COALESCE(""SkillCategory"", '')) <> ''
                    UNION ALL
                    SELECT TRIM(""Category"") AS name
                      FROM ""GigTasks""
                     WHERE TRIM(COALESCE(""Category"", '')) <> ''
                ) AS in_use
                ORDER BY LOWER(name), name;
            ");

            // A landing category for rows whose text was blank or unmatched.
            // Only created when there is actually something to land.
            migrationBuilder.Sql(@"
                INSERT INTO ""SkillCategories"" (""Name"", ""IsActive"", ""DisplayOrder"", ""CreatedAt"")
                SELECT 'General', TRUE, 999, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""SkillCategories"" WHERE LOWER(""Name"") = 'general')
                  AND (EXISTS (SELECT 1 FROM ""Partners"") OR EXISTS (SELECT 1 FROM ""GigTasks""));
            ");

            // ----------------------------------------- 3. nullable FK columns
            migrationBuilder.AddColumn<int>(
                name: "SkillCategoryId",
                table: "Partners",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "GigTasks",
                type: "integer",
                nullable: true);

            // ------------------------------------------------- 4. backfill
            migrationBuilder.Sql(@"
                UPDATE ""Partners"" p
                   SET ""SkillCategoryId"" = c.""Id""
                  FROM ""SkillCategories"" c
                 WHERE LOWER(c.""Name"") = LOWER(TRIM(p.""SkillCategory""));

                UPDATE ""GigTasks"" t
                   SET ""CategoryId"" = c.""Id""
                  FROM ""SkillCategories"" c
                 WHERE LOWER(c.""Name"") = LOWER(TRIM(t.""Category""));

                UPDATE ""Partners""
                   SET ""SkillCategoryId"" =
                       (SELECT ""Id"" FROM ""SkillCategories"" WHERE LOWER(""Name"") = 'general' LIMIT 1)
                 WHERE ""SkillCategoryId"" IS NULL;

                UPDATE ""GigTasks""
                   SET ""CategoryId"" =
                       (SELECT ""Id"" FROM ""SkillCategories"" WHERE LOWER(""Name"") = 'general' LIMIT 1)
                 WHERE ""CategoryId"" IS NULL;
            ");

            // ------------------------------------------------ 5. enforce NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "SkillCategoryId",
                table: "Partners",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "GigTasks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // --------------------------------- 6. retire the free-text columns
            migrationBuilder.DropColumn(name: "SkillCategory", table: "Partners");
            migrationBuilder.DropColumn(name: "Category", table: "GigTasks");

            // ------------------------------------------ 7. indexes and keys
            migrationBuilder.CreateIndex(
                name: "IX_Partners_SkillCategoryId",
                table: "Partners",
                column: "SkillCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GigTasks_CategoryId",
                table: "GigTasks",
                column: "CategoryId");

            // Restrict: a category in use must be deactivated, never deleted.
            migrationBuilder.AddForeignKey(
                name: "FK_Partners_SkillCategories_SkillCategoryId",
                table: "Partners",
                column: "SkillCategoryId",
                principalTable: "SkillCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GigTasks_SkillCategories_CategoryId",
                table: "GigTasks",
                column: "CategoryId",
                principalTable: "SkillCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Partners_SkillCategories_SkillCategoryId",
                table: "Partners");

            migrationBuilder.DropForeignKey(
                name: "FK_GigTasks_SkillCategories_CategoryId",
                table: "GigTasks");

            migrationBuilder.DropIndex(name: "IX_Partners_SkillCategoryId", table: "Partners");
            migrationBuilder.DropIndex(name: "IX_GigTasks_CategoryId", table: "GigTasks");

            migrationBuilder.AddColumn<string>(
                name: "SkillCategory",
                table: "Partners",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "GigTasks",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            // Put the names back so a rollback keeps the data readable.
            migrationBuilder.Sql(@"
                UPDATE ""Partners"" p
                   SET ""SkillCategory"" = c.""Name""
                  FROM ""SkillCategories"" c
                 WHERE c.""Id"" = p.""SkillCategoryId"";

                UPDATE ""GigTasks"" t
                   SET ""Category"" = c.""Name""
                  FROM ""SkillCategories"" c
                 WHERE c.""Id"" = t.""CategoryId"";
            ");

            migrationBuilder.DropColumn(name: "SkillCategoryId", table: "Partners");
            migrationBuilder.DropColumn(name: "CategoryId", table: "GigTasks");

            migrationBuilder.DropTable(name: "SkillCategories");
        }
    }
}
