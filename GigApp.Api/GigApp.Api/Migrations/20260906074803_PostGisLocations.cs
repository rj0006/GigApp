using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class PostGisLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<Point>(
                name: "BaseLocation",
                table: "Partners",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "GigTasks",
                type: "geography (point, 4326)",
                nullable: true);

            // Existing rows already carry the coordinates. Filling the geography
            // column from them here means matching works on day one instead of
            // only for work booked after this migration.
            migrationBuilder.Sql(@"
                UPDATE ""GigTasks""
                SET ""Location"" = ST_SetSRID(ST_MakePoint(""Longitude"", ""Latitude""), 4326)::geography
                WHERE ""Latitude"" IS NOT NULL AND ""Longitude"" IS NOT NULL;");

            migrationBuilder.Sql(@"
                UPDATE ""Partners""
                SET ""BaseLocation"" = ST_SetSRID(ST_MakePoint(""BaseLongitude"", ""BaseLatitude""), 4326)::geography
                WHERE ""BaseLatitude"" IS NOT NULL AND ""BaseLongitude"" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_BaseLocation",
                table: "Partners",
                column: "BaseLocation")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_GigTasks_Location",
                table: "GigTasks",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Partners_BaseLocation",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_GigTasks_Location",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "BaseLocation",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "GigTasks");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
