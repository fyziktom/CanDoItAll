using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmHrCrossModuleResponsibleParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsiblePartyId",
                table: "Validation_Runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsiblePartyId",
                table: "TestLab_TestPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintainerPartyId",
                table: "Resources_ProjectResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerPartyId",
                table: "Resources_ProjectResources",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponsiblePartyId",
                table: "Validation_Runs");

            migrationBuilder.DropColumn(
                name: "ResponsiblePartyId",
                table: "TestLab_TestPlans");

            migrationBuilder.DropColumn(
                name: "MaintainerPartyId",
                table: "Resources_ProjectResources");

            migrationBuilder.DropColumn(
                name: "OwnerPartyId",
                table: "Resources_ProjectResources");
        }
    }
}
