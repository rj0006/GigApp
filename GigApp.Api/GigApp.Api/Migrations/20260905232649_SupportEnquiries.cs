using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class SupportEnquiries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportEnquiries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GigTaskId = table.Column<int>(type: "integer", nullable: false),
                    RaisedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RaisedByRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Topic = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportEnquiries", x => x.Id);
                    table.CheckConstraint("CK_SupportEnquiries_RaisedByRole", "\"RaisedByRole\" IN ('customer','partner')");
                    table.CheckConstraint("CK_SupportEnquiries_Status", "\"Status\" IN ('open','in_progress','resolved')");
                    table.CheckConstraint("CK_SupportEnquiries_Topic", "\"Topic\" IN ('payment','quality','behaviour','timing','other')");
                    table.ForeignKey(
                        name: "FK_SupportEnquiries_GigTasks_GigTaskId",
                        column: x => x.GigTaskId,
                        principalTable: "GigTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportEnquiries_Users_RaisedByUserId",
                        column: x => x.RaisedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportEnquiries_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportEnquiries_GigTaskId_RaisedByUserId",
                table: "SupportEnquiries",
                columns: new[] { "GigTaskId", "RaisedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportEnquiries_RaisedByUserId",
                table: "SupportEnquiries",
                column: "RaisedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportEnquiries_ResolvedByUserId",
                table: "SupportEnquiries",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportEnquiries_Status_CreatedAt",
                table: "SupportEnquiries",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportEnquiries");
        }
    }
}
