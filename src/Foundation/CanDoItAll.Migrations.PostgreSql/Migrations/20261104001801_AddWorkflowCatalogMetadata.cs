using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddWorkflowCatalogMetadata : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "ExternalKey",
                table: "AgentFramework_WorkflowDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalNamespace",
                table: "AgentFramework_WorkflowDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceHash",
                table: "AgentFramework_WorkflowDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "AgentFramework_WorkflowDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TemplatePackKey",
                table: "AgentFramework_WorkflowDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TemplatePackVersion",
                table: "AgentFramework_WorkflowDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "AgentFramework_WorkflowDefinitions"
                SET "TemplateKey" = COALESCE("DefinitionJson"::jsonb ->> 'templateKey', ''),
                    "TemplatePackKey" = COALESCE("DefinitionJson"::jsonb ->> 'templatePackKey', ''),
                    "TemplatePackVersion" = COALESCE("DefinitionJson"::jsonb ->> 'templatePackVersion', ''),
                    "SourceHash" = COALESCE("DefinitionJson"::jsonb ->> 'sourceHash', ''),
                    "ExternalNamespace" = COALESCE("DefinitionJson"::jsonb ->> 'externalNamespace', ''),
                    "ExternalKey" = COALESCE("DefinitionJson"::jsonb ->> 'externalKey', '')
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "ExternalKey",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ExternalNamespace",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "SourceHash",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "TemplatePackKey",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "TemplatePackVersion",
                table: "AgentFramework_WorkflowDefinitions");
        }
    }
}
