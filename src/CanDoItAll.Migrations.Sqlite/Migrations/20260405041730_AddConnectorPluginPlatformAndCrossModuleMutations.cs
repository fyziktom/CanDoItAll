using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectorPluginPlatformAndCrossModuleMutations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigSchemaVersion",
                table: "Workspace_ProviderProfiles",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConnectorPluginKey",
                table: "Workspace_ProviderProfiles",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConfigSchemaVersion",
                table: "Resources_ProjectResources",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConnectorPluginKey",
                table: "Resources_ProjectResources",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectCrossModuleMutations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeNodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    MutationKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectCrossModuleMutations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ScopeNodeKey_CreatedAtUtc",
                table: "Workbench_ProjectCrossModuleMutations",
                columns: new[] { "ProjectId", "ScopeNodeKey", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_ProjectId_Status_UpdatedAtUtc",
                table: "Workbench_ProjectCrossModuleMutations",
                columns: new[] { "ProjectId", "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropColumn(
                name: "ConfigSchemaVersion",
                table: "Workspace_ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "ConnectorPluginKey",
                table: "Workspace_ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "ConfigSchemaVersion",
                table: "Resources_ProjectResources");

            migrationBuilder.DropColumn(
                name: "ConnectorPluginKey",
                table: "Resources_ProjectResources");
        }
    }
}
