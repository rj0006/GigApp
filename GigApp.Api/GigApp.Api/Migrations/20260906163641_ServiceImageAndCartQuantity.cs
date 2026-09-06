using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class ServiceImageAndCartQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageFileName",
                table: "ServiceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "GigTasks",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageFileName",
                table: "ServiceItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "GigTasks");
        }
    }
}
