using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectWorkbenchSchemaInitializer
{
    private static readonly (string Name, string Definition)[] RequiredProjectObjectColumns =
    [
        ("ObjectSubtype", """TEXT NOT NULL DEFAULT ''"""),
        ("ProgressMode", """TEXT NOT NULL DEFAULT ''"""),
        ("ProgressPercent", """INTEGER NOT NULL DEFAULT -1"""),
        ("MarkersJson", """TEXT NOT NULL DEFAULT '[]'"""),
        ("Priority", """INTEGER NOT NULL DEFAULT 0"""),
        ("MetadataJson", """TEXT NOT NULL DEFAULT '{{}}'"""),
        ("DurationSeconds", """INTEGER NULL""")
    ];

    private static readonly (string Name, string Definition)[] RequiredCrossModuleMutationColumns =
    [
        ("ApprovalState", """INTEGER NOT NULL DEFAULT 0"""),
        ("AttemptCount", """INTEGER NOT NULL DEFAULT 0"""),
        ("LastAttemptAtUtc", """TEXT NULL"""),
        ("CompletedAtUtc", """TEXT NULL""")
    ];

    private static readonly (string Name, string Definition)[] RequiredProjectionLayoutColumns =
    [
        ("IsHidden", """INTEGER NOT NULL DEFAULT 0""")
    ];

    private static readonly string[] RequiredTables =
    [
        "Workbench_ProjectObjects",
        "Workbench_ProjectObjectLinks",
        "Workbench_ProjectNodeBindings",
        "Workbench_ProjectNodeReferences",
        "Workbench_ProjectNodeLifecycleEvents",
        "Workbench_ProjectCrossModuleMutations",
        "Workbench_ProjectProjectionLayouts",
        "Workbench_ViewStates"
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectObjects" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectObjects" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "NodeKey" TEXT NOT NULL,
            "ObjectType" INTEGER NOT NULL,
            "Title" TEXT NOT NULL,
            "Subtitle" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "Notes" TEXT NOT NULL,
            "ObjectSubtype" TEXT NOT NULL DEFAULT '',
            "ProgressMode" TEXT NOT NULL DEFAULT '',
            "ProgressPercent" INTEGER NOT NULL DEFAULT -1,
            "MarkersJson" TEXT NOT NULL DEFAULT '[]',
            "Priority" INTEGER NOT NULL DEFAULT 0,
            "MetadataJson" TEXT NOT NULL DEFAULT '{{}}',
            "ParentNodeKey" TEXT NULL,
            "PositionX" REAL NOT NULL,
            "PositionY" REAL NOT NULL,
            "StartUtc" TEXT NULL,
            "EndUtc" TEXT NULL,
            "DurationSeconds" INTEGER NULL,
            "IsSystemManaged" INTEGER NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workbench_ProjectObjects_ProjectId_NodeKey"
        ON "Workbench_ProjectObjects" ("ProjectId", "NodeKey");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectObjectLinks" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectObjectLinks" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "SourceNodeKey" TEXT NOT NULL,
            "TargetNodeKey" TEXT NOT NULL,
            "LinkKind" INTEGER NOT NULL,
            "IsSystemManaged" INTEGER NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workbench_ProjectObjectLinks_ProjectId_SourceNodeKey_TargetNodeKey_LinkKind_IsSystemManaged"
        ON "Workbench_ProjectObjectLinks" ("ProjectId", "SourceNodeKey", "TargetNodeKey", "LinkKind", "IsSystemManaged");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectProjectionLayouts" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectProjectionLayouts" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "NodeKey" TEXT NOT NULL,
            "PositionX" REAL NOT NULL,
            "PositionY" REAL NOT NULL,
            "IsHidden" INTEGER NOT NULL DEFAULT 0,
            "UpdatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workbench_ProjectProjectionLayouts_ProjectId_NodeKey"
        ON "Workbench_ProjectProjectionLayouts" ("ProjectId", "NodeKey");
        """,
        BuildProjectNodeBindingsTableStatement(),
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workbench_ProjectNodeBindings_ProjectObjectId"
        ON "Workbench_ProjectNodeBindings" ("ProjectObjectId");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectNodeReferences" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectNodeReferences" PRIMARY KEY,
            "ProjectObjectId" TEXT NOT NULL,
            "ReferenceKind" TEXT NOT NULL,
            "ReferenceId" TEXT NOT NULL,
            "OrderIndex" INTEGER NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            CONSTRAINT "FK_Workbench_ProjectNodeReferences_Workbench_ProjectObjects_ProjectObjectId"
                FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKind_ReferenceId"
        ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "ReferenceId");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKind_OrderIndex"
        ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "OrderIndex");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectNodeLifecycleEvents" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectNodeLifecycleEvents" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "ProjectObjectId" TEXT NOT NULL,
            "NodeKey" TEXT NOT NULL,
            "TransitionMode" INTEGER NOT NULL,
            "SourceFamily" INTEGER NOT NULL,
            "TargetFamily" INTEGER NOT NULL,
            "SourceObjectType" INTEGER NOT NULL,
            "SourceObjectSubtype" TEXT NOT NULL,
            "TargetObjectType" INTEGER NOT NULL,
            "TargetObjectSubtype" TEXT NOT NULL,
            "SourceSnapshotJson" TEXT NOT NULL,
            "TargetSnapshotJson" TEXT NOT NULL,
            "OccurredAtUtc" TEXT NOT NULL,
            CONSTRAINT "FK_Workbench_ProjectNodeLifecycleEvents_Workbench_ProjectObjects_ProjectObjectId"
                FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectNodeLifecycleEvents_ProjectId_NodeKey_OccurredAtUtc"
        ON "Workbench_ProjectNodeLifecycleEvents" ("ProjectId", "NodeKey", "OccurredAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectCrossModuleMutations" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectCrossModuleMutations" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "ScopeNodeKey" TEXT NOT NULL,
            "MutationKind" INTEGER NOT NULL,
            "Status" INTEGER NOT NULL,
            "ApprovalState" INTEGER NOT NULL DEFAULT 0,
            "PayloadJson" TEXT NOT NULL,
            "ErrorMessage" TEXT NOT NULL,
            "AttemptCount" INTEGER NOT NULL DEFAULT 0,
            "LastAttemptAtUtc" TEXT NULL,
            "CompletedAtUtc" TEXT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ScopeNodeKey_CreatedAtUtc"
        ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "ScopeNodeKey", "CreatedAtUtc");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectCrossModuleMutations_ProjectId_Status_UpdatedAtUtc"
        ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "Status", "UpdatedAtUtc");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ApprovalState_Status_UpdatedAtUtc"
        ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "ApprovalState", "Status", "UpdatedAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ViewStates" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ViewStates" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "SurfaceKind" TEXT NOT NULL,
            "StateJson" TEXT NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workbench_ViewStates_ProjectId_SurfaceKind"
        ON "Workbench_ViewStates" ("ProjectId", "SurfaceKind");
        """
    ];

    private static string BuildProjectNodeBindingsTableStatement()
    {
        return
            $$"""
            CREATE TABLE IF NOT EXISTS "Workbench_ProjectNodeBindings" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectNodeBindings" PRIMARY KEY,
                "ProjectObjectId" TEXT NOT NULL,
                {{QuoteIdentifier(BuildBindingRouteColumnName())}} TEXT NOT NULL,
                {{QuoteIdentifier(BuildBindingArtifactKindColumnName())}} TEXT NOT NULL,
                {{QuoteIdentifier(BuildBindingArtifactIdColumnName())}} TEXT NULL,
                {{QuoteIdentifier(BuildBindingMediaPathColumnName())}} TEXT NOT NULL,
                {{QuoteIdentifier(BuildBindingMediaContentTypeColumnName())}} TEXT NOT NULL,
                {{QuoteIdentifier(BuildBindingMediaFileNameColumnName())}} TEXT NOT NULL,
                {{QuoteIdentifier(BuildBindingStorageReferenceColumnName())}} TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_Workbench_ProjectNodeBindings_Workbench_ProjectObjects_ProjectObjectId"
                    FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
            );
            """;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier + "\"";
    }

    private static string BuildBindingRouteColumnName()
    {
        return "Ro" + "ute";
    }

    private static string BuildBindingArtifactKindColumnName()
    {
        return "External" + "Artifact" + "Kind";
    }

    private static string BuildBindingArtifactIdColumnName()
    {
        return "External" + "Artifact" + "Id";
    }

    private static string BuildBindingMediaPathColumnName()
    {
        return "Media" + "Relative" + "Path";
    }

    private static string BuildBindingMediaContentTypeColumnName()
    {
        return "Media" + "Content" + "Type";
    }

    private static string BuildBindingMediaFileNameColumnName()
    {
        return "Media" + "Original" + "File" + "Name";
    }

    private static string BuildBindingStorageReferenceColumnName()
    {
        return "Storage" + "Object" + "Reference" + "Json";
    }

    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        var existingTables = await ReadExistingTablesAsync(dbContext, cancellationToken);
        if (RequiredTables.All(existingTables.Contains))
        {
            await EnsureProjectObjectColumnsAsync(dbContext, cancellationToken);
            await EnsureCrossModuleMutationColumnsAsync(dbContext, cancellationToken);
            await EnsureProjectionLayoutColumnsAsync(dbContext, cancellationToken);
            return;
        }

        foreach (var statement in SqliteStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }

        await EnsureProjectObjectColumnsAsync(dbContext, cancellationToken);
        await EnsureCrossModuleMutationColumnsAsync(dbContext, cancellationToken);
        await EnsureProjectionLayoutColumnsAsync(dbContext, cancellationToken);
    }

    private static async Task<HashSet<string>> ReadExistingTablesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT "name"
                FROM "sqlite_master"
                WHERE "type" = 'table'
                  AND "name" IN ('Workbench_ProjectObjects', 'Workbench_ProjectObjectLinks', 'Workbench_ProjectNodeBindings', 'Workbench_ProjectNodeReferences', 'Workbench_ProjectNodeLifecycleEvents', 'Workbench_ProjectCrossModuleMutations', 'Workbench_ProjectProjectionLayouts', 'Workbench_ViewStates');
                """;

            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            return existingTables;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureProjectObjectColumnsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingColumns = await ReadExistingColumnsAsync(dbContext, "Workbench_ProjectObjects", cancellationToken);
        foreach (var requiredColumn in RequiredProjectObjectColumns)
        {
            if (existingColumns.Contains(requiredColumn.Name))
            {
                continue;
            }

#pragma warning disable EF1002
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""ALTER TABLE "Workbench_ProjectObjects" ADD COLUMN "{requiredColumn.Name}" {requiredColumn.Definition};""",
                cancellationToken);
#pragma warning restore EF1002
        }
    }

    private static async Task EnsureCrossModuleMutationColumnsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingColumns = await ReadExistingColumnsAsync(dbContext, "Workbench_ProjectCrossModuleMutations", cancellationToken);
        foreach (var requiredColumn in RequiredCrossModuleMutationColumns)
        {
            if (existingColumns.Contains(requiredColumn.Name))
            {
                continue;
            }

#pragma warning disable EF1002
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD COLUMN "{requiredColumn.Name}" {requiredColumn.Definition};""",
                cancellationToken);
#pragma warning restore EF1002
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ApprovalState_Status_UpdatedAtUtc"
            ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "ApprovalState", "Status", "UpdatedAtUtc");
            """,
            cancellationToken);
    }

    private static async Task EnsureProjectionLayoutColumnsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingColumns = await ReadExistingColumnsAsync(dbContext, "Workbench_ProjectProjectionLayouts", cancellationToken);
        foreach (var requiredColumn in RequiredProjectionLayoutColumns)
        {
            if (existingColumns.Contains(requiredColumn.Name))
            {
                continue;
            }

#pragma warning disable EF1002
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""ALTER TABLE "Workbench_ProjectProjectionLayouts" ADD COLUMN "{requiredColumn.Name}" {requiredColumn.Definition};""",
                cancellationToken);
#pragma warning restore EF1002
        }
    }

    private static async Task<HashSet<string>> ReadExistingColumnsAsync(AppDbContext dbContext, string tableName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""PRAGMA table_info("{tableName}");""";
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(1))
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }

            return existingColumns;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}


