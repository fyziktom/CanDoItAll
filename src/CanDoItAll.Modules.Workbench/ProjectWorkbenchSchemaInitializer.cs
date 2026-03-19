using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectWorkbenchSchemaInitializer
{
    private static readonly string[] RequiredTables =
    [
        "Workbench_ProjectObjects",
        "Workbench_ProjectObjectLinks",
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
            "Route" TEXT NOT NULL,
            "ExternalArtifactKind" TEXT NOT NULL,
            "ExternalArtifactId" TEXT NULL,
            "ParentNodeKey" TEXT NULL,
            "PositionX" REAL NOT NULL,
            "PositionY" REAL NOT NULL,
            "StartUtc" TEXT NULL,
            "EndUtc" TEXT NULL,
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

    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        var existingTables = await ReadExistingTablesAsync(dbContext, cancellationToken);
        if (RequiredTables.All(existingTables.Contains))
        {
            return;
        }

        foreach (var statement in SqliteStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
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
                  AND "name" IN ('Workbench_ProjectObjects', 'Workbench_ProjectObjectLinks', 'Workbench_ViewStates');
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
}
