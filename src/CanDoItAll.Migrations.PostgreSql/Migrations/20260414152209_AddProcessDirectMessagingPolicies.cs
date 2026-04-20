using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessDirectMessagingPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsDirectMessaging",
                table: "Processes_RunAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Processes_RoleMessagingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleMessagingPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_DefinitionVersion~",
                        column: x => x.ProcessDefinitionVersionId,
                        principalTable: "Processes_DefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_RoleRequirements_~",
                        column: x => x.SourceRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_RoleRequirements~1",
                        column: x => x.TargetRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_ProcessDefinitionVersionId_~",
                table: "Processes_RoleMessagingPolicies",
                columns: new[] { "ProcessDefinitionVersionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_SourceRoleRequirementId",
                table: "Processes_RoleMessagingPolicies",
                column: "SourceRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_TargetRoleRequirementId",
                table: "Processes_RoleMessagingPolicies",
                column: "TargetRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRoleMessagingPolicies_SourceTarget",
                table: "Processes_RoleMessagingPolicies",
                columns: new[] { "ProcessDefinitionVersionId", "SourceRoleRequirementId", "TargetRoleRequirementId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_RoleMessagingPolicies");

            migrationBuilder.DropColumn(
                name: "AllowsDirectMessaging",
                table: "Processes_RunAssignments");
        }
    }
}
