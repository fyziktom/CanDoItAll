using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectStructureMcpSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workspace_ProjectStructureAgentProfiles");

            migrationBuilder.DropTable(
                name: "Workspace_ProjectStructureAgentProjectOverrides");

            migrationBuilder.DropTable(
                name: "Workspace_ProjectStructureAgentSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workspace_ProjectStructureAgentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessTokenCipherText = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovalRequiredMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoApproveMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CapabilityMask = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    RequireApprovalForAllMutations = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProjectStructureAgentProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ProjectStructureAgentProjectOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApprovalRequiredMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoApproveMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CapabilityMask = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RequireApprovalForAllMutations = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProjectStructureAgentProjectOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ProjectStructureAgentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CentralBaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DefaultApprovalRequiredMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultAutoApproveMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    InstallScriptPath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SetupReadmePath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProjectStructureAgentSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ProjectStructureAgentProjectOverrides_ProfileId_ProjectId",
                table: "Workspace_ProjectStructureAgentProjectOverrides",
                columns: new[] { "ProfileId", "ProjectId" },
                unique: true);
        }
    }
}
