using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBannerAndServiceItemZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BannerZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BannerId = table.Column<int>(type: "integer", nullable: false),
                    ServiceZoneId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannerZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BannerZones_Banners_BannerId",
                        column: x => x.BannerId,
                        principalTable: "Banners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BannerZones_ServiceZones_ServiceZoneId",
                        column: x => x.ServiceZoneId,
                        principalTable: "ServiceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceItemZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    ServiceZoneId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceItemZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceItemZones_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceItemZones_ServiceZones_ServiceZoneId",
                        column: x => x.ServiceZoneId,
                        principalTable: "ServiceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BannerZones_BannerId_ServiceZoneId",
                table: "BannerZones",
                columns: new[] { "BannerId", "ServiceZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BannerZones_ServiceZoneId",
                table: "BannerZones",
                column: "ServiceZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItemZones_ServiceItemId_ServiceZoneId",
                table: "ServiceItemZones",
                columns: new[] { "ServiceItemId", "ServiceZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItemZones_ServiceZoneId",
                table: "ServiceItemZones",
                column: "ServiceZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannerZones");

            migrationBuilder.DropTable(
                name: "ServiceItemZones");
        }
    }
}
