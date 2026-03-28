using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public static class WorkspaceSchemaInitializer
{
    private static readonly string[] RequiredTables =
    [
        "Workspace_ProjectStructureAgentSettings",
        "Workspace_ProjectStructureAgentProfiles",
        "Workspace_ProjectStructureAgentProjectOverrides"
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Workspace_ProjectStructureAgentSettings" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProjectStructureAgentSettings" PRIMARY KEY,
            "CentralBaseUrl" TEXT NOT NULL,
            "InstallScriptPath" TEXT NOT NULL,
            "SetupReadmePath" TEXT NOT NULL,
            "DefaultAutoApproveMinutes" INTEGER NOT NULL,
            "DefaultApprovalRequiredMinutes" INTEGER NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workspace_ProjectStructureAgentProfiles" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProjectStructureAgentProfiles" PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "AccessTokenCipherText" TEXT NOT NULL,
            "IsEnabled" INTEGER NOT NULL,
            "CapabilityMask" INTEGER NOT NULL,
            "AutoApproveMinutes" INTEGER NOT NULL,
            "ApprovalRequiredMinutes" INTEGER NOT NULL,
            "RequireApprovalForAllMutations" INTEGER NOT NULL,
            "Notes" TEXT NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workspace_ProjectStructureAgentProjectOverrides" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProjectStructureAgentProjectOverrides" PRIMARY KEY,
            "ProfileId" TEXT NOT NULL,
            "ProjectId" TEXT NOT NULL,
            "ProjectName" TEXT NOT NULL,
            "IsEnabled" INTEGER NOT NULL,
            "CapabilityMask" INTEGER NOT NULL,
            "AutoApproveMinutes" INTEGER NOT NULL,
            "ApprovalRequiredMinutes" INTEGER NOT NULL,
            "RequireApprovalForAllMutations" INTEGER NOT NULL,
            "Notes" TEXT NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workspace_ProjectStructureAgentProjectOverrides_ProfileId_ProjectId"
        ON "Workspace_ProjectStructureAgentProjectOverrides" ("ProfileId", "ProjectId");
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
                  AND "name" IN ('Workspace_ProjectStructureAgentSettings', 'Workspace_ProjectStructureAgentProfiles', 'Workspace_ProjectStructureAgentProjectOverrides');
                """;

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    tables.Add(reader.GetString(0));
                }
            }

            return tables;
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
