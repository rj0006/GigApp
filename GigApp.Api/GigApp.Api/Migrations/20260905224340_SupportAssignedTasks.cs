using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class SupportAssignedTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "GigTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedByUserId",
                table: "GigTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentNote",
                table: "GigTasks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigTasks_AssignedByUserId",
                table: "GigTasks",
                column: "AssignedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GigTasks_Users_AssignedByUserId",
                table: "GigTasks",
                column: "AssignedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GigTasks_Users_AssignedByUserId",
                table: "GigTasks");

            migrationBuilder.DropIndex(
                name: "IX_GigTasks_AssignedByUserId",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "AssignedByUserId",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "AssignmentNote",
                table: "GigTasks");
        }
    }
}
