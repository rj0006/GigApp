using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaskUrgencyAndBookingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scaffolded default was an empty string, which the check
            // constraints below would immediately reject for every existing row.
            // Existing tasks were all bid-based and none were marked urgent, so
            // those are the honest values to backfill.
            migrationBuilder.AddColumn<string>(
                name: "BookingMode",
                table: "GigTasks",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "bidding");

            migrationBuilder.AddColumn<string>(
                name: "Urgency",
                table: "GigTasks",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.CreateIndex(
                name: "IX_GigTasks_Status_Urgency",
                table: "GigTasks",
                columns: new[] { "Status", "Urgency" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GigTasks_BookingMode",
                table: "GigTasks",
                sql: "\"BookingMode\" IN ('bidding','instant')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GigTasks_Urgency",
                table: "GigTasks",
                sql: "\"Urgency\" IN ('urgent','normal','flexible')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GigTasks_Status_Urgency",
                table: "GigTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GigTasks_BookingMode",
                table: "GigTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GigTasks_Urgency",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "BookingMode",
                table: "GigTasks");

            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "GigTasks");
        }
    }
}
