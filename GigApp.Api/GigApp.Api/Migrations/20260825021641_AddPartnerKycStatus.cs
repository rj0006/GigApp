using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GigApp.Api.Migrations
{
    /// <summary>
    /// Replaces the IsVerified flag with a four-state KycStatus so that "never
    /// reviewed" and "rejected" stop looking identical. The scaffolded version
    /// dropped IsVerified before anything read it, which would have silently
    /// unverified every existing partner, and defaulted the new column to an
    /// empty string, which the check constraint rejects. Both are fixed here.
    /// </summary>
    public partial class AddPartnerKycStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KycStatus",
                table: "Partners",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "not_submitted");

            migrationBuilder.AddColumn<string>(
                name: "KycRejectionReason",
                table: "Partners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycReviewedAt",
                table: "Partners",
                type: "timestamp with time zone",
                nullable: true);

            // Carry the old flag across before it is dropped. A partner who was
            // verified stays verified; one who has every document on file but
            // was never approved goes back into the review queue rather than
            // looking like they never applied.
            migrationBuilder.Sql(@"
                UPDATE ""Partners""
                SET ""KycStatus"" = CASE
                    WHEN ""IsVerified"" THEN 'approved'
                    WHEN ""SelfieFileName"" IS NOT NULL
                     AND ""AadhaarFrontFileName"" IS NOT NULL
                     AND ""AadhaarBackFileName"" IS NOT NULL
                     AND COALESCE(TRIM(""AadhaarNumber""), '') <> '' THEN 'pending'
                    ELSE 'not_submitted'
                END;
            ");

            migrationBuilder.DropIndex(
                name: "IX_Partners_IsVerified_IsAvailable",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Partners");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_KycStatus_IsAvailable",
                table: "Partners",
                columns: new[] { "KycStatus", "IsAvailable" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Partners_KycStatus",
                table: "Partners",
                sql: "\"KycStatus\" IN ('not_submitted','pending','approved','rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Partners",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Only approved partners were verified under the old shape. The
            // rejection reason has nowhere to go and is lost on the way back.
            migrationBuilder.Sql(@"
                UPDATE ""Partners"" SET ""IsVerified"" = (""KycStatus"" = 'approved');
            ");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Partners_KycStatus",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_Partners_KycStatus_IsAvailable",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "KycRejectionReason",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "KycReviewedAt",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "Partners");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_IsVerified_IsAvailable",
                table: "Partners",
                columns: new[] { "IsVerified", "IsAvailable" });
        }
    }
}
