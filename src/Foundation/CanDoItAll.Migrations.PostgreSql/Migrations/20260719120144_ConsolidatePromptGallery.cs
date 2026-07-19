using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidatePromptGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceBlueprintId",
                table: "Prompts_PromptVersions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OutputFormat",
                table: "Prompts_PromptVersions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "KindSnapshot",
                table: "Prompts_PromptVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecommendedMaxOutputTokensSnapshot",
                table: "Prompts_PromptVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RecommendedTemperatureSnapshot",
                table: "Prompts_PromptVersions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RecommendedTopPSnapshot",
                table: "Prompts_PromptVersions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummarySnapshot",
                table: "Prompts_PromptVersions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TitleSnapshot",
                table: "Prompts_PromptVersions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "Prompts_PromptArtifacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Prompts_PromptArtifacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Prompts_PromptArtifacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Provenance",
                table: "Prompts_PromptArtifacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecommendedMaxOutputTokens",
                table: "Prompts_PromptArtifacts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RecommendedTemperature",
                table: "Prompts_PromptArtifacts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RecommendedTopP",
                table: "Prompts_PromptArtifacts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCatalog",
                table: "Prompts_PromptArtifacts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                table: "Prompts_PromptArtifacts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceGroupKey",
                table: "Prompts_PromptArtifacts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceGroupName",
                table: "Prompts_PromptArtifacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceItemKind",
                table: "Prompts_PromptArtifacts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Prompts_PromptArtifacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceOrderIndex",
                table: "Prompts_PromptArtifacts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Prompts_PromptArtifacts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Prompts_PromptCompatibilityWarningPreferences",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<int>(type: "integer", nullable: false),
                    IssueCode = table.Column<int>(type: "integer", nullable: false),
                    IsSuppressed = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptCompatibilityWarningPreferences", x => new { x.PromptArtifactId, x.Consumer, x.IssueCode });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptCompatibilityWarningPreferences_Prompts_Promp~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptSupportedConsumers",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptSupportedConsumers", x => new { x.PromptArtifactId, x.Consumer });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptSupportedConsumers_Prompts_PromptArtifacts_Pr~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptSupportedProviderModels",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptSupportedProviderModels", x => new { x.PromptArtifactId, x.ProviderKey, x.ModelKey });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptSupportedProviderModels_Prompts_PromptArtifac~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptTemplateTokens",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptTemplateTokens", x => new { x.PromptArtifactId, x.NameKey });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptTemplateTokens_Prompts_PromptArtifacts_Prompt~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                UPDATE "Prompts_PromptVersions" AS version
                SET
                    "TitleSnapshot" = artifact."Title",
                    "SummarySnapshot" = artifact."Summary",
                    "KindSnapshot" = artifact."Kind",
                    "RecommendedTemperatureSnapshot" = artifact."RecommendedTemperature",
                    "RecommendedMaxOutputTokensSnapshot" = artifact."RecommendedMaxOutputTokens",
                    "RecommendedTopPSnapshot" = artifact."RecommendedTopP"
                FROM "Prompts_PromptArtifacts" AS artifact
                WHERE version."PromptArtifactId" = artifact."Id";

                INSERT INTO "Prompts_PromptArtifacts" (
                    "Id",
                    "ProjectId",
                    "CollectionId",
                    "Title",
                    "Summary",
                    "Kind",
                    "Phase",
                    "Status",
                    "CurrentDraftText",
                    "CurrentVersionNumber",
                    "Provenance",
                    "SourceKey",
                    "SourceCatalog",
                    "SourceGroupKey",
                    "SourceGroupName",
                    "SourceItemKind",
                    "SourceOrderIndex",
                    "SourceFingerprint",
                    "RecommendedTemperature",
                    "RecommendedMaxOutputTokens",
                    "RecommendedTopP",
                    "IsArchived",
                    "ArchivedAtUtc",
                    "CreatedAtUtc",
                    "UpdatedAtUtc")
                SELECT
                    block."Id",
                    NULL,
                    NULL,
                    block."Name",
                    block."Summary",
                    1,
                    LEFT(block."PhaseRules", 80),
                    1,
                    block."Content",
                    1,
                    2,
                    LEFT('legacy-factory-block:' || block."Id"::text || ':' || block."Key", 200),
                    COALESCE(NULLIF(TRIM(block."CatalogSource"), ''), 'legacy-prompt-factory'),
                    NULLIF(TRIM(block."GroupKey"), ''),
                    NULLIF(TRIM(block."GroupKey"), ''),
                    CASE block."BlockKind"
                        WHEN 0 THEN 'Instruction'
                        WHEN 1 THEN 'Constraint'
                        WHEN 2 THEN 'Validation'
                        WHEN 3 THEN 'Delivery'
                        WHEN 4 THEN 'Security'
                        WHEN 5 THEN 'Testing'
                        ELSE 'Unknown'
                    END,
                    block."OrderIndex",
                    UPPER(MD5(CONCAT_WS(E'\x1f', block."Key", block."Name", block."Content"))),
                    NULL,
                    NULL,
                    NULL,
                    FALSE,
                    NULL,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM "Factory_PromptBlocks" AS block
                WHERE LOWER(TRIM(COALESCE(block."CatalogSource", ''))) <> 'prompt-library-pack'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Prompts_PromptArtifacts" AS artifact
                      WHERE artifact."Id" = block."Id");

                INSERT INTO "Prompts_PromptVersions" (
                    "Id",
                    "PromptArtifactId",
                    "VersionNumber",
                    "Content",
                    "CreationReason",
                    "OutputFormat",
                    "SourceBlueprintId",
                    "TitleSnapshot",
                    "SummarySnapshot",
                    "KindSnapshot",
                    "RecommendedTemperatureSnapshot",
                    "RecommendedMaxOutputTokensSnapshot",
                    "RecommendedTopPSnapshot",
                    "CreatedAtUtc")
                SELECT
                    MD5('legacy-factory-version:' || block."Id"::text)::uuid,
                    block."Id",
                    1,
                    block."Content",
                    'Migrated from Prompt Factory during Gallery consolidation.',
                    'Markdown',
                    NULL,
                    block."Name",
                    block."Summary",
                    1,
                    NULL,
                    NULL,
                    NULL,
                    CURRENT_TIMESTAMP
                FROM "Factory_PromptBlocks" AS block
                INNER JOIN "Prompts_PromptArtifacts" AS artifact
                    ON artifact."Id" = block."Id"
                   AND artifact."Provenance" = 2
                   AND artifact."SourceKey" = LEFT('legacy-factory-block:' || block."Id"::text || ':' || block."Key", 200)
                WHERE LOWER(TRIM(COALESCE(block."CatalogSource", ''))) <> 'prompt-library-pack'
                ON CONFLICT DO NOTHING;

                WITH legacy_tags AS (
                    SELECT DISTINCT ON (UPPER(LEFT(TRIM(tag."Value"), 120)))
                        LEFT(TRIM(tag."Value"), 120) AS "Name"
                    FROM "Factory_PromptBlocks" AS block
                    CROSS JOIN LATERAL JSONB_ARRAY_ELEMENTS_TEXT(
                        COALESCE(NULLIF(TRIM(block."TagsJson"), ''), '[]')::jsonb ||
                        COALESCE(NULLIF(TRIM(block."StackTagsJson"), ''), '[]')::jsonb) AS tag("Value")
                    INNER JOIN "Prompts_PromptArtifacts" AS artifact
                        ON artifact."Id" = block."Id"
                       AND artifact."Provenance" = 2
                    WHERE LOWER(TRIM(COALESCE(block."CatalogSource", ''))) <> 'prompt-library-pack'
                      AND TRIM(tag."Value") <> ''
                    ORDER BY UPPER(LEFT(TRIM(tag."Value"), 120)), LEFT(TRIM(tag."Value"), 120)
                )
                INSERT INTO "Prompts_PromptTags" ("Id", "Name")
                SELECT
                    MD5('legacy-prompt-tag:' || UPPER(tag."Name"))::uuid,
                    tag."Name"
                FROM legacy_tags AS tag
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Prompts_PromptTags" AS existing
                    WHERE UPPER(existing."Name") = UPPER(tag."Name"))
                ON CONFLICT DO NOTHING;

                WITH block_tags AS (
                    SELECT DISTINCT
                        block."Id" AS "PromptArtifactId",
                        UPPER(LEFT(TRIM(tag."Value"), 120)) AS "TagKey"
                    FROM "Factory_PromptBlocks" AS block
                    CROSS JOIN LATERAL JSONB_ARRAY_ELEMENTS_TEXT(
                        COALESCE(NULLIF(TRIM(block."TagsJson"), ''), '[]')::jsonb ||
                        COALESCE(NULLIF(TRIM(block."StackTagsJson"), ''), '[]')::jsonb) AS tag("Value")
                    INNER JOIN "Prompts_PromptArtifacts" AS artifact
                        ON artifact."Id" = block."Id"
                       AND artifact."Provenance" = 2
                    WHERE LOWER(TRIM(COALESCE(block."CatalogSource", ''))) <> 'prompt-library-pack'
                      AND TRIM(tag."Value") <> ''
                )
                INSERT INTO "Prompts_PromptArtifactTags" ("PromptArtifactId", "PromptTagId")
                SELECT block_tag."PromptArtifactId", prompt_tag."Id"
                FROM block_tags AS block_tag
                CROSS JOIN LATERAL (
                    SELECT tag."Id"
                    FROM "Prompts_PromptTags" AS tag
                    WHERE UPPER(tag."Name") = block_tag."TagKey"
                    ORDER BY tag."Id"
                    LIMIT 1) AS prompt_tag
                ON CONFLICT DO NOTHING;

                WITH block_tokens AS (
                    SELECT DISTINCT
                        block."Id" AS "PromptArtifactId",
                        LEFT(TRIM(token."Value"), 200) AS "Name",
                        UPPER(LEFT(TRIM(token."Value"), 200)) AS "NameKey"
                    FROM "Factory_PromptBlocks" AS block
                    CROSS JOIN LATERAL JSONB_ARRAY_ELEMENTS_TEXT(
                        COALESCE(NULLIF(TRIM(block."TemplateTokensJson"), ''), '[]')::jsonb) AS token("Value")
                    INNER JOIN "Prompts_PromptArtifacts" AS artifact
                        ON artifact."Id" = block."Id"
                       AND artifact."Provenance" = 2
                    WHERE LOWER(TRIM(COALESCE(block."CatalogSource", ''))) <> 'prompt-library-pack'
                      AND TRIM(token."Value") <> ''
                )
                INSERT INTO "Prompts_PromptTemplateTokens" ("PromptArtifactId", "NameKey", "Name")
                SELECT token."PromptArtifactId", token."NameKey", token."Name"
                FROM block_tokens AS token
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropTable(
                name: "Factory_PromptBlocks");

            migrationBuilder.DropTable(
                name: "Factory_PromptBlueprints");

            migrationBuilder.DropTable(
                name: "Factory_PromptBuildSessions");

            migrationBuilder.DropTable(
                name: "Factory_PromptFlowTemplates");

            migrationBuilder.DropTable(
                name: "Factory_PromptRunNodes");

            migrationBuilder.DropTable(
                name: "Factory_PromptRuns");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptUsageRecords_PromptArtifactId",
                table: "Prompts_PromptUsageRecords",
                column: "PromptArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifactTags_PromptTagId",
                table: "Prompts_PromptArtifactTags",
                column: "PromptTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_Status_Kind_UpdatedAtUtc",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "IsArchived", "Status", "Kind", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_Provenance_SourceKey",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "Provenance", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptSupportedProviderModels_ProviderKey_ModelKey_~",
                table: "Prompts_PromptSupportedProviderModels",
                columns: new[] { "ProviderKey", "ModelKey", "PromptArtifactId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Prompts_PromptArtifactTags_Prompts_PromptArtifacts_PromptAr~",
                table: "Prompts_PromptArtifactTags",
                column: "PromptArtifactId",
                principalTable: "Prompts_PromptArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Prompts_PromptArtifactTags_Prompts_PromptTags_PromptTagId",
                table: "Prompts_PromptArtifactTags",
                column: "PromptTagId",
                principalTable: "Prompts_PromptTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prompts_PromptUsageRecords_Prompts_PromptArtifacts_PromptAr~",
                table: "Prompts_PromptUsageRecords",
                column: "PromptArtifactId",
                principalTable: "Prompts_PromptArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prompts_PromptVersions_Prompts_PromptArtifacts_PromptArtifa~",
                table: "Prompts_PromptVersions",
                column: "PromptArtifactId",
                principalTable: "Prompts_PromptArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prompts_PromptArtifactTags_Prompts_PromptArtifacts_PromptAr~",
                table: "Prompts_PromptArtifactTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Prompts_PromptArtifactTags_Prompts_PromptTags_PromptTagId",
                table: "Prompts_PromptArtifactTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Prompts_PromptUsageRecords_Prompts_PromptArtifacts_PromptAr~",
                table: "Prompts_PromptUsageRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Prompts_PromptVersions_Prompts_PromptArtifacts_PromptArtifa~",
                table: "Prompts_PromptVersions");

            migrationBuilder.DropTable(
                name: "Prompts_PromptCompatibilityWarningPreferences");

            migrationBuilder.DropTable(
                name: "Prompts_PromptSupportedConsumers");

            migrationBuilder.DropTable(
                name: "Prompts_PromptSupportedProviderModels");

            migrationBuilder.DropTable(
                name: "Prompts_PromptTemplateTokens");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptUsageRecords_PromptArtifactId",
                table: "Prompts_PromptUsageRecords");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptArtifactTags_PromptTagId",
                table: "Prompts_PromptArtifactTags");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_Status_Kind_UpdatedAtUtc",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptArtifacts_Provenance_SourceKey",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "KindSnapshot",
                table: "Prompts_PromptVersions");

            migrationBuilder.DropColumn(
                name: "RecommendedMaxOutputTokensSnapshot",
                table: "Prompts_PromptVersions");

            migrationBuilder.DropColumn(
                name: "RecommendedTemperatureSnapshot",
                table: "Prompts_PromptVersions");

            migrationBuilder.DropColumn(
                name: "RecommendedTopPSnapshot",
                table: "Prompts_PromptVersions");

            migrationBuilder.DropColumn(
                name: "SummarySnapshot",
                table: "Prompts_PromptVersions");

            migrationBuilder.DropColumn(
                name: "TitleSnapshot",
                table: "Prompts_PromptVersions");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "Provenance",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "RecommendedMaxOutputTokens",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "RecommendedTemperature",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "RecommendedTopP",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "SourceCatalog",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "SourceGroupKey",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "SourceGroupName",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "SourceItemKind",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "SourceOrderIndex",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.AlterColumn<string>(
                name: "SourceBlueprintId",
                table: "Prompts_PromptVersions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OutputFormat",
                table: "Prompts_PromptVersions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.CreateTable(
                name: "Factory_PromptBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockKind = table.Column<int>(type: "integer", nullable: false),
                    BlueprintRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CatalogSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    GroupKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsRecommendedByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    PhaseRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PromptTypeRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StackTagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    TemplateTokensJson = table.Column<string>(type: "TEXT", nullable: false),
                    ToolboxEligible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBlueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Guidance = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    PromptType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RecommendedBlockKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedFlowKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    RecommendedFlowTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBlueprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBuildSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlueprintId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CanvasUiStateJson = table.Column<string>(type: "TEXT", nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ComponentCustomizationsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FlowTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    HasCustomizedBlocks = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepositoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SelectedBlockIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedPromptRunNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedResourceIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SessionAttachmentsJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WarningSummary = table.Column<string>(type: "TEXT", nullable: false),
                    WizardStepIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBuildSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptFlowTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentSequenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    BlockIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    BlockKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    CatalogSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    PromptTypeRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptFlowTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptRunNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BranchLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ParentPromptRunNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptBlockDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptRunNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FlowTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptRuns", x => x.Id);
                });
        }
    }
}
