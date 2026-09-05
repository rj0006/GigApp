using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaskRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "Users",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TaskRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GigTaskId = table.Column<int>(type: "integer", nullable: false),
                    RaterUserId = table.Column<int>(type: "integer", nullable: false),
                    RaterRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskRatings", x => x.Id);
                    table.CheckConstraint("CK_TaskRatings_RaterRole", "\"RaterRole\" IN ('customer','partner')");
                    table.CheckConstraint("CK_TaskRatings_Stars", "\"Stars\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_TaskRatings_GigTasks_GigTaskId",
                        column: x => x.GigTaskId,
                        principalTable: "GigTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskRatings_Users_RaterUserId",
                        column: x => x.RaterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskRatings_GigTaskId_RaterRole",
                table: "TaskRatings",
                columns: new[] { "GigTaskId", "RaterRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskRatings_RaterUserId",
                table: "TaskRatings",
                column: "RaterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskRatings");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "Users");
        }
    }
}
