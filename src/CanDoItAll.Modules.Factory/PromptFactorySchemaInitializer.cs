using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Factory;

public static class PromptFactorySchemaInitializer
{
    private sealed record RequiredColumn(string Name, string AddColumnSql);

    private static readonly IReadOnlyDictionary<string, string> CreateTableStatements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Factory_PromptBlocks"] =
            """
            CREATE TABLE IF NOT EXISTS "Factory_PromptBlocks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptBlocks" PRIMARY KEY,
                "Key" TEXT NOT NULL DEFAULT '',
                "Name" TEXT NOT NULL,
                "BlockKind" INTEGER NOT NULL,
                "Summary" TEXT NOT NULL DEFAULT '',
                "Content" TEXT NOT NULL DEFAULT '',
                "IsRecommendedByDefault" INTEGER NOT NULL DEFAULT 0,
                "PromptTypeRules" TEXT NOT NULL DEFAULT '',
                "BlueprintRules" TEXT NOT NULL DEFAULT '',
                "PhaseRules" TEXT NOT NULL DEFAULT '',
                "GroupKey" TEXT NOT NULL DEFAULT '',
                "TagsJson" TEXT NOT NULL DEFAULT '[]',
                "StackTagsJson" TEXT NOT NULL DEFAULT '[]',
                "TemplateTokensJson" TEXT NOT NULL DEFAULT '[]',
                "ToolboxEligible" INTEGER NOT NULL DEFAULT 0,
                "OrderIndex" INTEGER NOT NULL DEFAULT 0,
                "CatalogSource" TEXT NOT NULL DEFAULT ''
            );
            """,
        ["Factory_PromptFlowTemplates"] =
            """
            CREATE TABLE IF NOT EXISTS "Factory_PromptFlowTemplates" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptFlowTemplates" PRIMARY KEY,
                "Key" TEXT NOT NULL DEFAULT '',
                "Name" TEXT NOT NULL,
                "Summary" TEXT NOT NULL DEFAULT '',
                "BlockIdsJson" TEXT NOT NULL DEFAULT '[]',
                "BlockKeysJson" TEXT NOT NULL DEFAULT '[]',
                "AgentSequenceJson" TEXT NOT NULL DEFAULT '[]',
                "PromptTypeRules" TEXT NOT NULL DEFAULT '',
                "OrderIndex" INTEGER NOT NULL DEFAULT 0,
                "CatalogSource" TEXT NOT NULL DEFAULT ''
            );
            """,
        ["Factory_PromptBlueprints"] =
            """
            CREATE TABLE IF NOT EXISTS "Factory_PromptBlueprints" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptBlueprints" PRIMARY KEY,
                "Key" TEXT NOT NULL DEFAULT '',
                "Name" TEXT NOT NULL,
                "PromptType" TEXT NOT NULL DEFAULT '',
                "Summary" TEXT NOT NULL DEFAULT '',
                "Guidance" TEXT NOT NULL DEFAULT '',
                "RecommendedFlowTemplateId" TEXT NULL,
                "RecommendedFlowKey" TEXT NOT NULL DEFAULT '',
                "RecommendedBlockKeysJson" TEXT NOT NULL DEFAULT '[]',
                "OrderIndex" INTEGER NOT NULL DEFAULT 0,
                "CatalogSource" TEXT NOT NULL DEFAULT ''
            );
            """,
        ["Factory_PromptRuns"] =
            """
            CREATE TABLE IF NOT EXISTS "Factory_PromptRuns" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptRuns" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "FlowTemplateId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Phase" TEXT NOT NULL DEFAULT '',
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """,
        ["Factory_PromptRunNodes"] =
            """
            CREATE TABLE IF NOT EXISTS "Factory_PromptRunNodes" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptRunNodes" PRIMARY KEY,
                "PromptRunId" TEXT NOT NULL,
                "PromptBlockDefinitionId" TEXT NULL,
                "PromptArtifactId" TEXT NULL,
                "ParentPromptRunNodeId" TEXT NULL,
                "Title" TEXT NOT NULL,
                "BranchKey" TEXT NOT NULL DEFAULT 'main',
                "BranchLabel" TEXT NOT NULL DEFAULT 'Main',
                "Sequence" INTEGER NOT NULL DEFAULT 0,
                "State" INTEGER NOT NULL DEFAULT 0,
                "Notes" TEXT NOT NULL DEFAULT ''
            );
            """,
        ["Factory_PromptBuildSessions"] =
            """
            CREATE TABLE IF NOT EXISTS "Factory_PromptBuildSessions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptBuildSessions" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "ProjectId" TEXT NULL,
                "Phase" TEXT NOT NULL DEFAULT '',
                "BlueprintId" TEXT NULL,
                "FlowTemplateId" TEXT NULL,
                "ProviderProfileId" TEXT NULL,
                "PromptArtifactId" TEXT NULL,
                "PromptRunId" TEXT NULL,
                "SelectedPromptRunNodeId" TEXT NULL,
                "RepositoryName" TEXT NOT NULL DEFAULT '',
                "BranchName" TEXT NOT NULL DEFAULT '',
                "CommitSha" TEXT NOT NULL DEFAULT '',
                "SelectedBlockIdsJson" TEXT NOT NULL DEFAULT '[]',
                "SelectedResourceIdsJson" TEXT NOT NULL DEFAULT '[]',
                "GeneratedPrompt" TEXT NOT NULL DEFAULT '',
                "WarningSummary" TEXT NOT NULL DEFAULT '',
                "CanvasUiStateJson" TEXT NOT NULL DEFAULT '{{}}',
                "ComponentCustomizationsJson" TEXT NOT NULL DEFAULT '[]',
                "SessionAttachmentsJson" TEXT NOT NULL DEFAULT '[]',
                "WizardStepIndex" INTEGER NOT NULL DEFAULT 0,
                "HasCustomizedBlocks" INTEGER NOT NULL DEFAULT 0,
                "UpdatedAtUtc" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00+00:00'
            );
            """
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<RequiredColumn>> RequiredColumns = new Dictionary<string, IReadOnlyList<RequiredColumn>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Factory_PromptBlocks"] =
        [
            new("Key", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "Key" TEXT NOT NULL DEFAULT '';"""),
            new("Name", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "Name" TEXT NOT NULL DEFAULT '';"""),
            new("BlockKind", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "BlockKind" INTEGER NOT NULL DEFAULT 0;"""),
            new("Summary", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "Summary" TEXT NOT NULL DEFAULT '';"""),
            new("Content", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "Content" TEXT NOT NULL DEFAULT '';"""),
            new("IsRecommendedByDefault", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "IsRecommendedByDefault" INTEGER NOT NULL DEFAULT 0;"""),
            new("PromptTypeRules", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "PromptTypeRules" TEXT NOT NULL DEFAULT '';"""),
            new("BlueprintRules", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "BlueprintRules" TEXT NOT NULL DEFAULT '';"""),
            new("PhaseRules", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "PhaseRules" TEXT NOT NULL DEFAULT '';"""),
            new("GroupKey", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "GroupKey" TEXT NOT NULL DEFAULT '';"""),
            new("TagsJson", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "TagsJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("StackTagsJson", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "StackTagsJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("TemplateTokensJson", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "TemplateTokensJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("ToolboxEligible", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "ToolboxEligible" INTEGER NOT NULL DEFAULT 0;"""),
            new("OrderIndex", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "OrderIndex" INTEGER NOT NULL DEFAULT 0;"""),
            new("CatalogSource", """ALTER TABLE "Factory_PromptBlocks" ADD COLUMN "CatalogSource" TEXT NOT NULL DEFAULT '';""")
        ],
        ["Factory_PromptFlowTemplates"] =
        [
            new("Key", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "Key" TEXT NOT NULL DEFAULT '';"""),
            new("Name", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "Name" TEXT NOT NULL DEFAULT '';"""),
            new("Summary", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "Summary" TEXT NOT NULL DEFAULT '';"""),
            new("BlockIdsJson", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "BlockIdsJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("BlockKeysJson", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "BlockKeysJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("AgentSequenceJson", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "AgentSequenceJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("PromptTypeRules", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "PromptTypeRules" TEXT NOT NULL DEFAULT '';"""),
            new("OrderIndex", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "OrderIndex" INTEGER NOT NULL DEFAULT 0;"""),
            new("CatalogSource", """ALTER TABLE "Factory_PromptFlowTemplates" ADD COLUMN "CatalogSource" TEXT NOT NULL DEFAULT '';""")
        ],
        ["Factory_PromptBlueprints"] =
        [
            new("Key", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "Key" TEXT NOT NULL DEFAULT '';"""),
            new("Name", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "Name" TEXT NOT NULL DEFAULT '';"""),
            new("PromptType", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "PromptType" TEXT NOT NULL DEFAULT '';"""),
            new("Summary", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "Summary" TEXT NOT NULL DEFAULT '';"""),
            new("Guidance", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "Guidance" TEXT NOT NULL DEFAULT '';"""),
            new("RecommendedFlowTemplateId", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "RecommendedFlowTemplateId" TEXT NULL;"""),
            new("RecommendedFlowKey", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "RecommendedFlowKey" TEXT NOT NULL DEFAULT '';"""),
            new("RecommendedBlockKeysJson", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "RecommendedBlockKeysJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("OrderIndex", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "OrderIndex" INTEGER NOT NULL DEFAULT 0;"""),
            new("CatalogSource", """ALTER TABLE "Factory_PromptBlueprints" ADD COLUMN "CatalogSource" TEXT NOT NULL DEFAULT '';""")
        ],
        ["Factory_PromptRuns"] =
        [
            new("ProjectId", """ALTER TABLE "Factory_PromptRuns" ADD COLUMN "ProjectId" TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';"""),
            new("FlowTemplateId", """ALTER TABLE "Factory_PromptRuns" ADD COLUMN "FlowTemplateId" TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';"""),
            new("Name", """ALTER TABLE "Factory_PromptRuns" ADD COLUMN "Name" TEXT NOT NULL DEFAULT '';"""),
            new("Phase", """ALTER TABLE "Factory_PromptRuns" ADD COLUMN "Phase" TEXT NOT NULL DEFAULT '';"""),
            new("CreatedAtUtc", """ALTER TABLE "Factory_PromptRuns" ADD COLUMN "CreatedAtUtc" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00+00:00';"""),
            new("UpdatedAtUtc", """ALTER TABLE "Factory_PromptRuns" ADD COLUMN "UpdatedAtUtc" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00+00:00';""")
        ],
        ["Factory_PromptRunNodes"] =
        [
            new("PromptRunId", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "PromptRunId" TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';"""),
            new("PromptBlockDefinitionId", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "PromptBlockDefinitionId" TEXT NULL;"""),
            new("PromptArtifactId", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "PromptArtifactId" TEXT NULL;"""),
            new("ParentPromptRunNodeId", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "ParentPromptRunNodeId" TEXT NULL;"""),
            new("Title", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "Title" TEXT NOT NULL DEFAULT '';"""),
            new("BranchKey", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "BranchKey" TEXT NOT NULL DEFAULT 'main';"""),
            new("BranchLabel", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "BranchLabel" TEXT NOT NULL DEFAULT 'Main';"""),
            new("Sequence", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "Sequence" INTEGER NOT NULL DEFAULT 0;"""),
            new("State", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "State" INTEGER NOT NULL DEFAULT 0;"""),
            new("Notes", """ALTER TABLE "Factory_PromptRunNodes" ADD COLUMN "Notes" TEXT NOT NULL DEFAULT '';""")
        ],
        ["Factory_PromptBuildSessions"] =
        [
            new("Name", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "Name" TEXT NOT NULL DEFAULT '';"""),
            new("ProjectId", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "ProjectId" TEXT NULL;"""),
            new("Phase", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "Phase" TEXT NOT NULL DEFAULT '';"""),
            new("BlueprintId", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "BlueprintId" TEXT NULL;"""),
            new("FlowTemplateId", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "FlowTemplateId" TEXT NULL;"""),
            new("ProviderProfileId", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "ProviderProfileId" TEXT NULL;"""),
            new("PromptArtifactId", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "PromptArtifactId" TEXT NULL;"""),
            new("PromptRunId", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "PromptRunId" TEXT NULL;"""),
            new("SelectedPromptRunNodeId", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "SelectedPromptRunNodeId" TEXT NULL;"""),
            new("RepositoryName", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "RepositoryName" TEXT NOT NULL DEFAULT '';"""),
            new("BranchName", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "BranchName" TEXT NOT NULL DEFAULT '';"""),
            new("CommitSha", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "CommitSha" TEXT NOT NULL DEFAULT '';"""),
            new("SelectedBlockIdsJson", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "SelectedBlockIdsJson" TEXT NOT NULL DEFAULT '[]';"""),
new("SelectedResourceIdsJson", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "SelectedResourceIdsJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("GeneratedPrompt", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "GeneratedPrompt" TEXT NOT NULL DEFAULT '';"""),
            new("WarningSummary", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "WarningSummary" TEXT NOT NULL DEFAULT '';"""),
            new("CanvasUiStateJson", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "CanvasUiStateJson" TEXT NOT NULL DEFAULT '{{}}';"""),
            new("ComponentCustomizationsJson", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "ComponentCustomizationsJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("SessionAttachmentsJson", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "SessionAttachmentsJson" TEXT NOT NULL DEFAULT '[]';"""),
            new("WizardStepIndex", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "WizardStepIndex" INTEGER NOT NULL DEFAULT 0;"""),
            new("HasCustomizedBlocks", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "HasCustomizedBlocks" INTEGER NOT NULL DEFAULT 0;"""),
            new("UpdatedAtUtc", """ALTER TABLE "Factory_PromptBuildSessions" ADD COLUMN "UpdatedAtUtc" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00+00:00';""")
        ]
    };

    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        foreach (var statement in CreateTableStatements.Values)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var table in RequiredColumns)
            {
                var existingColumns = await ReadExistingColumnsAsync(connection, table.Key, cancellationToken);
                foreach (var column in table.Value)
                {
                    if (!existingColumns.Contains(column.Name))
                    {
                        await dbContext.Database.ExecuteSqlRawAsync(column.AddColumnSql, cancellationToken);
                    }
                }
            }
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<string>> ReadExistingColumnsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""PRAGMA table_info("{tableName}");""";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(1))
            {
                columns.Add(reader.GetString(1));
            }
        }

        return columns;
    }
}


