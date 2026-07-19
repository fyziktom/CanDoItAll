using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OptimizePromptGallerySearchAndWorkflowBindings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "Prompts_PromptTags",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Prompts_PromptArtifacts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "InstructionSnapshotSchemaVersion",
                table: "AgentFramework_WorkflowDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PromptArtifactId",
                table: "AgentFramework_WorkflowComponents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromptGalleryBindingSchemaVersion",
                table: "AgentFramework_WorkflowComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PromptVersionId",
                table: "AgentFramework_WorkflowComponents",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Prompts_PromptTags"
                SET "NameKey" = UPPER(BTRIM("Name"));
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Prompts_PromptArtifacts"
                SET "SearchText" = UPPER(CONCAT(
                    COALESCE("Title", ''), E'\n',
                    COALESCE("Summary", ''), E'\n',
                    COALESCE("Phase", ''), E'\n',
                    COALESCE("CurrentDraftText", ''), E'\n',
                    COALESCE("SourceKey", ''), E'\n',
                    COALESCE("SourceCatalog", ''), E'\n',
                    COALESCE("SourceGroupKey", ''), E'\n',
                    COALESCE("SourceGroupName", ''), E'\n',
                    COALESCE("SourceItemKind", '')));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptTags_NameKey",
                table: "Prompts_PromptTags",
                column: "NameKey");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_UpdatedAtUtc_Title_Id",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "IsArchived", "UpdatedAtUtc", "Title", "Id" },
                descending: new[] { false, true, false, false });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_InstructionSnapshotSchema_Id",
                table: "AgentFramework_WorkflowDefinitions",
                columns: new[] { "InstructionSnapshotSchemaVersion", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_PromptVersionId",
                table: "AgentFramework_WorkflowComponents",
                column: "PromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowComponents_PromptBinding",
                table: "AgentFramework_WorkflowComponents",
                columns: new[] { "PromptArtifactId", "PromptVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowComponents_PromptGalleryBindingSchema_Id",
                table: "AgentFramework_WorkflowComponents",
                columns: new[] { "PromptGalleryBindingSchemaVersion", "Id" });

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Prompts_PromptArtifacts_SearchText_Trgm"
                ON "Prompts_PromptArtifacts" USING GIN ("SearchText" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Prompts_PromptTags_NameKey_Trgm"
                ON "Prompts_PromptTags" USING GIN ("NameKey" gin_trgm_ops);
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentFramework_WorkflowComponents_Prompts_PromptArtifacts_P~",
                table: "AgentFramework_WorkflowComponents",
                column: "PromptArtifactId",
                principalTable: "Prompts_PromptArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentFramework_WorkflowComponents_Prompts_PromptVersions_Pr~",
                table: "AgentFramework_WorkflowComponents",
                column: "PromptVersionId",
                principalTable: "Prompts_PromptVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Prompts_PromptTags_NameKey_Trgm\";");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Prompts_PromptArtifacts_SearchText_Trgm\";");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentFramework_WorkflowComponents_Prompts_PromptArtifacts_P~",
                table: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentFramework_WorkflowComponents_Prompts_PromptVersions_Pr~",
                table: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptTags_NameKey",
                table: "Prompts_PromptTags");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_UpdatedAtUtc_Title_Id",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_InstructionSnapshotSchema_Id",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AgentFramework_WorkflowComponents_PromptVersionId",
                table: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowComponents_PromptBinding",
                table: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowComponents_PromptGalleryBindingSchema_Id",
                table: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "Prompts_PromptTags");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "InstructionSnapshotSchemaVersion",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "PromptArtifactId",
                table: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropColumn(
                name: "PromptGalleryBindingSchemaVersion",
                table: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropColumn(
                name: "PromptVersionId",
                table: "AgentFramework_WorkflowComponents");
        }
    }
}
