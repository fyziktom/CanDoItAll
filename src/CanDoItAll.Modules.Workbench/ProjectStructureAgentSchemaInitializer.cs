using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureAgentSchemaInitializer
{
    private static readonly string[] RequiredTables =
    [
        "Workbench_ProjectStructureLeases",
        "Workbench_ProjectStructureOperationAnalytics"
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectStructureLeases" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectStructureLeases" PRIMARY KEY,
            "ScopeKind" INTEGER NOT NULL,
            "ScopeKey" TEXT NOT NULL,
            "LeaseToken" TEXT NOT NULL,
            "AgentId" TEXT NOT NULL,
            "AgentName" TEXT NOT NULL,
            "MachineName" TEXT NOT NULL,
            "RepositoryRoot" TEXT NOT NULL,
            "BranchName" TEXT NOT NULL,
            "Reason" TEXT NOT NULL,
            "AcquiredAtUtc" TEXT NOT NULL,
            "RenewedAtUtc" TEXT NOT NULL,
            "ExpiresAtUtc" TEXT NOT NULL,
            "ReleasedAtUtc" TEXT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workbench_ProjectStructureLeases_LeaseToken"
        ON "Workbench_ProjectStructureLeases" ("LeaseToken");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectStructureLeases_ScopeKind_ScopeKey"
        ON "Workbench_ProjectStructureLeases" ("ScopeKind", "ScopeKey");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workbench_ProjectStructureOperationAnalytics" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectStructureOperationAnalytics" PRIMARY KEY,
            "OperationName" TEXT NOT NULL,
            "ProjectId" TEXT NULL,
            "NodeKey" TEXT NULL,
            "ScopeKind" INTEGER NULL,
            "ScopeKey" TEXT NULL,
            "AgentId" TEXT NOT NULL,
            "AgentName" TEXT NOT NULL,
            "MachineName" TEXT NOT NULL,
            "RepositoryRoot" TEXT NOT NULL,
            "BranchName" TEXT NOT NULL,
            "Succeeded" INTEGER NOT NULL,
            "DurationMs" INTEGER NOT NULL,
            "WarningCount" INTEGER NOT NULL,
            "ErrorCode" TEXT NULL,
            "ErrorMessage" TEXT NULL,
            "RequestSummaryJson" TEXT NOT NULL,
            "ResponseSummaryJson" TEXT NOT NULL,
            "WarningsJson" TEXT NOT NULL,
            "OccurredAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectStructureOperationAnalytics_OccurredAtUtc"
        ON "Workbench_ProjectStructureOperationAnalytics" ("OccurredAtUtc");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workbench_ProjectStructureOperationAnalytics_ProjectId_OperationName"
        ON "Workbench_ProjectStructureOperationAnalytics" ("ProjectId", "OperationName");
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
                  AND "name" IN ('Workbench_ProjectStructureLeases', 'Workbench_ProjectStructureOperationAnalytics');
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
