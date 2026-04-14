using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessDefinitionLifecycleInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status",
                table: "Processes_DefinitionVersions");

            migrationBuilder.AddColumn<int>(
                name: "NextVersionNumber",
                table: "Processes_Definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "UX_ProcessVersions_DraftPerDef",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessVersions_PubPerDef",
                table: "Processes_DefinitionVersions",
                column: "ProcessDefinitionId",
                unique: true,
                filter: "\"Status\" = 'Published'");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_ActivePublishedVersionId",
                table: "Processes_Definitions",
                column: "ActivePublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_Id_ActivePublishedVersionId",
                table: "Processes_Definitions",
                columns: new[] { "Id", "ActivePublishedVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Definitions_Processes_DefinitionVersions_Id_Activ~",
                table: "Processes_Definitions",
                columns: new[] { "Id", "ActivePublishedVersionId" },
                principalTable: "Processes_DefinitionVersions",
                principalColumns: new[] { "ProcessDefinitionId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Processes_Definitions_Processes_DefinitionVersions_Id_Activ~",
                table: "Processes_Definitions");

            migrationBuilder.DropIndex(
                name: "UX_ProcessVersions_DraftPerDef",
                table: "Processes_DefinitionVersions");

            migrationBuilder.DropIndex(
                name: "UX_ProcessVersions_PubPerDef",
                table: "Processes_DefinitionVersions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_Definitions_ActivePublishedVersionId",
                table: "Processes_Definitions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_Definitions_Id_ActivePublishedVersionId",
                table: "Processes_Definitions");

            migrationBuilder.DropColumn(
                name: "NextVersionNumber",
                table: "Processes_Definitions");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "Status" });
        }
    }
}
