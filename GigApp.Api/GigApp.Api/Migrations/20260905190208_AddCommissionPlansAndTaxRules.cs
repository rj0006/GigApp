using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GigApp.Api.Migrations
{
    public partial class AddCommissionPlansAndTaxRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LifetimeTax",
                table: "PartnerWallets",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedPercent",
                table: "LedgerEntries",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                table: "LedgerEntries",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommissionPlanId",
                table: "LedgerEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxRuleId",
                table: "LedgerEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommissionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CommissionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SubscriptionFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    BillingPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionPlans", x => x.Id);
                    table.CheckConstraint("CK_CommissionPlans_Fee", "\"SubscriptionFee\" >= 0");
                    table.CheckConstraint("CK_CommissionPlans_Percent", "\"CommissionPercent\" >= 0 AND \"CommissionPercent\" <= 100");
                });

            migrationBuilder.CreateTable(
                name: "TaxRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AppliesTo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRules", x => x.Id);
                    table.CheckConstraint("CK_TaxRules_AppliesTo", "\"AppliesTo\" IN ('commission','gross_earning','subscription_fee')");
                    table.CheckConstraint("CK_TaxRules_Percent", "\"Percent\" >= 0 AND \"Percent\" <= 100");
                });

            migrationBuilder.CreateTable(
                name: "PartnerPlanSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerId = table.Column<int>(type: "integer", nullable: false),
                    CommissionPlanId = table.Column<int>(type: "integer", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FeeCharged = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    StartsOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Remark = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AssignedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerPlanSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerPlanSubscriptions_CommissionPlans_CommissionPlanId",
                        column: x => x.CommissionPlanId,
                        principalTable: "CommissionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartnerPlanSubscriptions_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_CommissionPlanId",
                table: "LedgerEntries",
                column: "CommissionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TaxRuleId",
                table: "LedgerEntries",
                column: "TaxRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPlans_Code",
                table: "CommissionPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerPlanSubscriptions_CommissionPlanId",
                table: "PartnerPlanSubscriptions",
                column: "CommissionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerPlanSubscriptions_PartnerId_IsActive",
                table: "PartnerPlanSubscriptions",
                columns: new[] { "PartnerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_CountryCode_Code",
                table: "TaxRules",
                columns: new[] { "CountryCode", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_CountryCode_IsActive_SortOrder",
                table: "TaxRules",
                columns: new[] { "CountryCode", "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_CommissionPlans_CommissionPlanId",
                table: "LedgerEntries",
                column: "CommissionPlanId",
                principalTable: "CommissionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_TaxRules_TaxRuleId",
                table: "LedgerEntries",
                column: "TaxRuleId",
                principalTable: "TaxRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_CommissionPlans_CommissionPlanId",
                table: "LedgerEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_TaxRules_TaxRuleId",
                table: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "PartnerPlanSubscriptions");

            migrationBuilder.DropTable(
                name: "TaxRules");

            migrationBuilder.DropTable(
                name: "CommissionPlans");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_CommissionPlanId",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_TaxRuleId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "LifetimeTax",
                table: "PartnerWallets");

            migrationBuilder.DropColumn(
                name: "AppliedPercent",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "CommissionPlanId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "TaxRuleId",
                table: "LedgerEntries");
        }
    }
}
