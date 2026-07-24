using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class ImproveCrmHrRecordSelectionAndRecognitionIntegrity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Parties_DisplayName",
                table: "CrmHr_Parties");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId",
                table: "CrmHr_Opportunities");

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "CrmHr_PartyContactPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<decimal>(
                name: "RecognizedAmount",
                table: "CrmHr_OpportunityStageHistory",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecognizedCurrencyCode",
                table: "CrmHr_OpportunityStageHistory",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Projects_Name_Id",
                table: "Projects_Projects",
                columns: new[] { "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_DisplayName_Id",
                table: "CrmHr_Parties",
                columns: new[] { "DisplayName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId_Stage",
                table: "CrmHr_Opportunities",
                columns: new[] { "AccountPartyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId_UpdatedAtUtc_Id",
                table: "CrmHr_Opportunities",
                columns: new[] { "AccountPartyId", "UpdatedAtUtc", "Id" },
                descending: new[] { false, true, false });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Projects_Name_Id",
                table: "Projects_Projects");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Parties_DisplayName_Id",
                table: "CrmHr_Parties");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId_Stage",
                table: "CrmHr_Opportunities");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId_UpdatedAtUtc_Id",
                table: "CrmHr_Opportunities");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "CrmHr_PartyContactPoints");

            migrationBuilder.DropColumn(
                name: "RecognizedAmount",
                table: "CrmHr_OpportunityStageHistory");

            migrationBuilder.DropColumn(
                name: "RecognizedCurrencyCode",
                table: "CrmHr_OpportunityStageHistory");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_DisplayName",
                table: "CrmHr_Parties",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId",
                table: "CrmHr_Opportunities",
                column: "AccountPartyId");
        }
    }
}
