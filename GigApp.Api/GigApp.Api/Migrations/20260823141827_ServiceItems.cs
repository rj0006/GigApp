using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class ServiceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceItemId",
                table: "GigTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SkillCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BasePayout = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AllowsInstantBooking = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceItems_SkillCategories_SkillCategoryId",
                        column: x => x.SkillCategoryId,
                        principalTable: "SkillCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GigTasks_ServiceItemId_Status",
                table: "GigTasks",
                columns: new[] { "ServiceItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_SkillCategoryId_IsActive_DisplayOrder",
                table: "ServiceItems",
                columns: new[] { "SkillCategoryId", "IsActive", "DisplayOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_GigTasks_ServiceItems_ServiceItemId",
                table: "GigTasks",
                column: "ServiceItemId",
                principalTable: "ServiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // "Tap repair" and "tap repair" must not both exist inside the same
            // category. EF cannot express a LOWER(...) index, so it is created
            // directly — same approach as UX_SkillCategories_Name_Lower.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ""UX_ServiceItems_Category_Name_Lower""
                    ON ""ServiceItems"" (""SkillCategoryId"", LOWER(""Name""));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GigTasks_ServiceItems_ServiceItemId",
                table: "GigTasks");

            migrationBuilder.DropTable(
                name: "ServiceItems");

            migrationBuilder.DropIndex(
                name: "IX_GigTasks_ServiceItemId_Status",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "ServiceItemId",
                table: "GigTasks");
        }
    }
}
