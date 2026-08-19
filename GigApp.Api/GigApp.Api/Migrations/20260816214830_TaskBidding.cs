using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaskBidding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcceptedBidId",
                table: "GigTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedAmount",
                table: "GigTasks",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskBids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GigTaskId = table.Column<int>(type: "integer", nullable: false),
                    PartnerId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CounterAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CounterNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskBids", x => x.Id);
                    table.CheckConstraint("CK_TaskBids_Status", "\"Status\" IN ('pending','countered','accepted','rejected','withdrawn')");
                    table.ForeignKey(
                        name: "FK_TaskBids_GigTasks_GigTaskId",
                        column: x => x.GigTaskId,
                        principalTable: "GigTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskBids_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskBids_GigTaskId_PartnerId",
                table: "TaskBids",
                columns: new[] { "GigTaskId", "PartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskBids_PartnerId_Status",
                table: "TaskBids",
                columns: new[] { "PartnerId", "Status" });

            // Tasks assigned before bidding existed have no winning bid to take
            // an amount from. Their budget was the agreed price at the time, so
            // carry it across — otherwise every historic job reads as unpriced.
            migrationBuilder.Sql(@"
                UPDATE ""GigTasks""
                   SET ""AgreedAmount"" = ""Budget""
                 WHERE ""PartnerId"" IS NOT NULL
                   AND ""AgreedAmount"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskBids");

            migrationBuilder.DropColumn(
                name: "AcceptedBidId",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "AgreedAmount",
                table: "GigTasks");
        }
    }
}
