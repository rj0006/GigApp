using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class KycImagesAndProfilePhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KycDocumentUrl",
                table: "Partners");

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageFileName",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AadhaarBackFileName",
                table: "Partners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AadhaarFrontFileName",
                table: "Partners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AadhaarNumber",
                table: "Partners",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfieFileName",
                table: "Partners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileImageFileName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AadhaarBackFileName",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "AadhaarFrontFileName",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "AadhaarNumber",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "SelfieFileName",
                table: "Partners");

            migrationBuilder.AddColumn<string>(
                name: "KycDocumentUrl",
                table: "Partners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
