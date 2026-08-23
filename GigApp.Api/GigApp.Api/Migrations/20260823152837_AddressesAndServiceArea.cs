using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddressesAndServiceArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseCity",
                table: "Partners",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BaseLatitude",
                table: "Partners",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BaseLongitude",
                table: "Partners",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BasePincode",
                table: "Partners",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceRadiusKm",
                table: "Partners",
                type: "integer",
                nullable: false,
                // Scaffolded as 0, which would mean existing partners travel
                // nowhere and match no work at all. 10 km matches the model
                // default and is a sane starting radius.
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "GigTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "GigTasks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "GigTasks",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HouseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Landmark = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Pincode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.CheckConstraint("CK_Addresses_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)");
                    table.ForeignKey(
                        name: "FK_Addresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GigTasks_AddressId",
                table: "GigTasks",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserId_IsDeleted",
                table: "Addresses",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_GigTasks_Addresses_AddressId",
                table: "GigTasks",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GigTasks_Addresses_AddressId",
                table: "GigTasks");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_GigTasks_AddressId",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "BaseCity",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "BaseLatitude",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "BaseLongitude",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "BasePincode",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "ServiceRadiusKm",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "GigTasks");
        }
    }
}
