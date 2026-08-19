using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class TrackingLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackingLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FormType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DocNo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DocDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Remark = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingLogs", x => x.Id);
                    table.CheckConstraint("CK_TrackingLogs_EntryType", "\"EntryType\" IN ('insert','update','delete')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingLogs_FormType_DocNo",
                table: "TrackingLogs",
                columns: new[] { "FormType", "DocNo" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingLogs_TransactionDate",
                table: "TrackingLogs",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingLogs_UserId_TransactionDate",
                table: "TrackingLogs",
                columns: new[] { "UserId", "TransactionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackingLogs");
        }
    }
}
