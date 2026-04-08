using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public static class ConnectorCommandSchemaInitializer
{
    private static readonly string[] RequiredTables =
    [
        "Workspace_ConnectorCommands",
        "Workspace_ConnectorCommandAudits"
    ];

    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Workspace_ConnectorCommands" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ConnectorCommands" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "ConnectorPluginKey" TEXT NOT NULL,
            "CommandKey" TEXT NOT NULL,
            "IdempotencyKey" TEXT NOT NULL,
            "PayloadJson" TEXT NOT NULL,
            "Status" INTEGER NOT NULL,
            "ApprovalState" INTEGER NOT NULL,
            "AttemptCount" INTEGER NOT NULL,
            "LastAttemptAtUtc" TEXT NULL,
            "NextAttemptAtUtc" TEXT NULL,
            "CompletedAtUtc" TEXT NULL,
            "LastError" TEXT NOT NULL,
            "ResultJson" TEXT NOT NULL,
            "LeaseToken" TEXT NOT NULL DEFAULT '',
            "LeaseExpiresAtUtc" TEXT NULL,
            "RequestedBy" TEXT NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommands_ProjectId_ConnectorPluginKey_CommandKey_IdempotencyKey"
        ON "Workspace_ConnectorCommands" ("ProjectId", "ConnectorPluginKey", "CommandKey", "IdempotencyKey");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc_LeaseExpiresAtUtc"
        ON "Workspace_ConnectorCommands" ("Status", "ApprovalState", "NextAttemptAtUtc", "LeaseExpiresAtUtc");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommands_ProjectId_CreatedAtUtc"
        ON "Workspace_ConnectorCommands" ("ProjectId", "CreatedAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Workspace_ConnectorCommandAudits" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ConnectorCommandAudits" PRIMARY KEY,
            "ConnectorCommandId" TEXT NOT NULL,
            "ProjectId" TEXT NOT NULL,
            "EventKind" INTEGER NOT NULL,
            "Actor" TEXT NOT NULL,
            "Message" TEXT NOT NULL,
            "DetailsJson" TEXT NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            CONSTRAINT "FK_Workspace_ConnectorCommandAudits_Workspace_ConnectorCommands_ConnectorCommandId"
                FOREIGN KEY ("ConnectorCommandId") REFERENCES "Workspace_ConnectorCommands" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommandAudits_ConnectorCommandId_CreatedAtUtc"
        ON "Workspace_ConnectorCommandAudits" ("ConnectorCommandId", "CreatedAtUtc");
        """
    ];

    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        var existingTables = await ReadExistingTablesAsync(dbContext, cancellationToken);
        if (!RequiredTables.All(existingTables.Contains))
        {
            foreach (var statement in SqliteStatements)
            {
                await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
            }

            return;
        }

        var existingColumns = await ReadExistingColumnsAsync(
            dbContext,
            "Workspace_ConnectorCommands",
            cancellationToken);
        if (!existingColumns.Contains("LeaseToken"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Workspace_ConnectorCommands" ADD COLUMN "LeaseToken" TEXT NOT NULL DEFAULT '';""",
                cancellationToken);
        }

        if (!existingColumns.Contains("LeaseExpiresAtUtc"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Workspace_ConnectorCommands" ADD COLUMN "LeaseExpiresAtUtc" TEXT NULL;""",
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc_LeaseExpiresAtUtc"
            ON "Workspace_ConnectorCommands" ("Status", "ApprovalState", "NextAttemptAtUtc", "LeaseExpiresAtUtc");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommands_ProjectId_CreatedAtUtc"
            ON "Workspace_ConnectorCommands" ("ProjectId", "CreatedAtUtc");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommandAudits_ConnectorCommandId_CreatedAtUtc"
            ON "Workspace_ConnectorCommandAudits" ("ConnectorCommandId", "CreatedAtUtc");
            """,
            cancellationToken);
    }

    private static async Task<HashSet<string>> ReadExistingTablesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
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
                  AND "name" IN ('Workspace_ConnectorCommands', 'Workspace_ConnectorCommandAudits');
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

    private static async Task<HashSet<string>> ReadExistingColumnsAsync(
        AppDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
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
