using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class SuperAdminAndAccountStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Users",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "boolean",
                nullable: false,
                // Scaffolded as false, which would have locked every existing
                // account out of the product on deploy. Everyone who exists
                // today is active.
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role_IsActive",
                table: "Users",
                columns: new[] { "Role", "IsActive" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "\"Role\" IN ('customer','partner','admin','superadmin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Role_IsActive",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "\"Role\" IN ('customer','partner','admin')");
        }
    }
}
