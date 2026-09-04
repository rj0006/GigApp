using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GigApp.Api.Migrations
{
    public partial class AllowOneAccountPerRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "KycReviewNote",
                table: "Partners",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email_Role",
                table: "Users",
                columns: new[] { "Email", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone_Role",
                table: "Users",
                columns: new[] { "Phone", "Role" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email_Role",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone_Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycReviewNote",
                table: "Partners");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true);
        }
    }
}
