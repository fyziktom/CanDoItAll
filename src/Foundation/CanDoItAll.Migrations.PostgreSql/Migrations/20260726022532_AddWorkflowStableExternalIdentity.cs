using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowStableExternalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalKey",
                table: "AgentFramework_WorkflowDefinitionHeads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalNamespace",
                table: "AgentFramework_WorkflowDefinitionHeads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionHeads_ExternalIdentity",
                table: "AgentFramework_WorkflowDefinitionHeads",
                columns: new[] { "ExternalNamespace", "ExternalKey" },
                unique: true,
                filter: "\"ExternalNamespace\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitionHeads_ExternalIdentity",
                table: "AgentFramework_WorkflowDefinitionHeads");

            migrationBuilder.DropColumn(
                name: "ExternalKey",
                table: "AgentFramework_WorkflowDefinitionHeads");

            migrationBuilder.DropColumn(
                name: "ExternalNamespace",
                table: "AgentFramework_WorkflowDefinitionHeads");
        }
    }
}
