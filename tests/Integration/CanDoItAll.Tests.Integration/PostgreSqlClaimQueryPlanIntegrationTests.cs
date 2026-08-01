using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PostgreSqlClaimQueryPlanIntegrationTests {
    private const string ProcessOutboxClaimIndex = "IX_process_outbox_messages_Status_AvailableAtUtc_LockedAtUtc";
    private const string ProcessStepDispatchHeaderIndex = "IX_process_runtime_steps_RunId_Status";
    private const string ConnectorCommandClaimIndex = "IX_Workspace_ConnectorCommands_PendingClaimOrder";

    [Fact]
    public async Task PostgreSql_hot_claim_queries_use_dedicated_indexes() {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-claim-indexes");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("claim-indexes");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        await AssertIndexesExistAsync(dbContext);
        await SeedClaimPlanDatasetsAsync(dbContext);

        var processOutboxPlan = await ExplainAsync(dbContext, ProcessOutboxClaimSql);
        await WritePlanAsync("process-outbox-claim", processOutboxPlan);
        AssertPlanUsesIndex(processOutboxPlan, ProcessOutboxClaimIndex, "process_outbox_messages");

        var processStepDispatchPlan = await ExplainAsync(dbContext, ProcessStepDispatchHeaderSql);
        await WritePlanAsync("process-step-dispatch-header", processStepDispatchPlan);
        AssertPlanUsesIndex(processStepDispatchPlan, ProcessStepDispatchHeaderIndex, "process_runtime_steps");

        var connectorCommandPlan = await ExplainAsync(dbContext, ConnectorCommandClaimSql);
        await WritePlanAsync("connector-command-claim", connectorCommandPlan);
        AssertPlanUsesIndex(connectorCommandPlan, ConnectorCommandClaimIndex, "Workspace_ConnectorCommands");
    }

    private static async Task AssertIndexesExistAsync(AppDbContext dbContext) {
        var expected = new HashSet<string>(
            [
                ProcessOutboxClaimIndex,
                ProcessStepDispatchHeaderIndex,
                ConnectorCommandClaimIndex
            ],
            StringComparer.Ordinal);

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN (
                  'IX_process_outbox_messages_Status_AvailableAtUtc_LockedAtUtc',
                  'IX_process_runtime_steps_RunId_Status',
                  'IX_Workspace_ConnectorCommands_PendingClaimOrder'
              );
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            expected.Remove(reader.GetString(0));
        }

        Assert.Empty(expected);
    }

    private static async Task SeedClaimPlanDatasetsAsync(AppDbContext dbContext) {
        await dbContext.Database.ExecuteSqlRawAsync(SeedProcessOutboxSql);
        await dbContext.Database.ExecuteSqlRawAsync(SeedProcessStepDispatchSql);
        await dbContext.Database.ExecuteSqlRawAsync(SeedConnectorCommandsSql);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ANALYZE "process_outbox_messages";
            ANALYZE "process_runtime_steps";
            ANALYZE "Workspace_ConnectorCommands";
            """);
    }

    private static async Task<string> ExplainAsync(AppDbContext dbContext, string sql) {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN (ANALYZE, BUFFERS){Environment.NewLine}{sql}";
        await using var reader = await command.ExecuteReaderAsync();

        var lines = new List<string>();
        while (await reader.ReadAsync()) {
            lines.Add(reader.GetString(0));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static async Task WritePlanAsync(string planName, string plan) {
        var directory = Environment.GetEnvironmentVariable("CANDOITALL_Scenario05_QUERY_PLAN_DIR");
        if (string.IsNullOrWhiteSpace(directory)) {
            return;
        }

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"{planName}.txt"), plan);
    }

    private static void AssertPlanUsesIndex(string plan, string indexName, string tableName) {
        Assert.Contains(indexName, plan, StringComparison.Ordinal);
        Assert.DoesNotContain($"Seq Scan on {tableName}", plan, StringComparison.Ordinal);
        Assert.DoesNotContain($"Seq Scan on \"{tableName}\"", plan, StringComparison.Ordinal);
    }

    private const string SeedProcessOutboxSql =
        """
        INSERT INTO "process_outbox_messages" (
            "MessageId",
            "EventId",
            "SubscriberKind",
            "PayloadHash",
            "Status",
            "AttemptCount",
            "CreatedAtUtc",
            "AvailableAtUtc",
            "LockedAtUtc",
            "LockId",
            "DeliveredAtUtc",
            "LastErrorClass")
        SELECT
            ('00000000-0000-0000-1000-' || lpad(series.value::text, 12, '0'))::uuid,
            ('00000000-0000-0000-2000-' || lpad(series.value::text, 12, '0'))::uuid,
            'RuntimeProjection',
            'payload-' || series.value,
            CASE WHEN series.value <= 200 THEN 'Pending' ELSE 'Delivered' END,
            0,
            now() - make_interval(secs => series.value),
            CASE
                WHEN series.value <= 200 AND series.value % 11 = 0 THEN now() + interval '1 day'
                WHEN series.value <= 200 THEN now() - interval '5 minutes'
                ELSE NULL
            END,
            NULL,
            NULL,
            CASE WHEN series.value <= 200 THEN NULL ELSE now() - interval '1 hour' END,
            NULL
        FROM generate_series(1, 20000) AS series(value);
        """;

    private const string SeedProcessStepDispatchSql =
        """
        INSERT INTO "process_runtime_states" (
            "RunId",
            "RootRunId",
            "PlanId",
            "PlanHash",
            "Status",
            "UpdatedAtUtc",
            "ConcurrencyToken")
        SELECT
            ('10000000-0000-0000-0000-' || lpad(run.value::text, 12, '0'))::uuid,
            ('10000000-0000-0000-0000-' || lpad(run.value::text, 12, '0'))::uuid,
            ('20000000-0000-0000-0000-' || lpad(run.value::text, 12, '0'))::uuid,
            'plan-' || run.value,
            CASE WHEN run.value = 5 THEN 'Active' ELSE 'Completed' END,
            now() - make_interval(secs => run.value),
            ('30000000-0000-0000-0000-' || lpad(run.value::text, 12, '0'))::uuid
        FROM generate_series(1, 200) AS run(value);

        INSERT INTO "process_runtime_steps" (
            "RunId",
            "StepInstanceId",
            "StepDefinitionId",
            "Status",
            "IsExecutable",
            "AttemptNumber",
            "DependencyStepIds",
            "RequiredArtifactSlotIds",
            "ActiveClaimToken",
            "CompletedResultKey")
        SELECT
            ('10000000-0000-0000-0000-' || lpad(run.value::text, 12, '0'))::uuid,
            ('40000000-0000-0000-0000-' || lpad((run.value * 10000 + step.value)::text, 12, '0'))::uuid,
            ('50000000-0000-0000-0000-' || lpad(step.value::text, 12, '0'))::uuid,
            CASE
                WHEN step.value % 7 = 0 THEN 'Completed'
                WHEN step.value % 5 = 0 THEN 'Running'
                WHEN step.value % 3 = 0 THEN 'WaitingApproval'
                ELSE 'Ready'
            END,
            true,
            step.value,
            '[]',
            '[]',
            NULL,
            NULL
        FROM generate_series(1, 200) AS run(value)
        CROSS JOIN generate_series(1, 50) AS step(value);
        """;

    private const string SeedConnectorCommandsSql =
        """
        INSERT INTO "Workspace_ConnectorCommands" (
            "Id",
            "ProjectId",
            "ConnectorPluginKey",
            "CommandKey",
            "IdempotencyKey",
            "PayloadJson",
            "Status",
            "ApprovalState",
            "AttemptCount",
            "LastError",
            "ResultJson",
            "LeaseToken",
            "RequestedBy",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "NextAttemptAtUtc",
            "LeaseExpiresAtUtc")
        SELECT
            ('00000000-0000-0000-7000-' || lpad(series.value::text, 12, '0'))::uuid,
            '20000000-0000-0000-0000-000000000001',
            'scenario05-plugin',
            'deliver',
            'idem-' || series.value,
            '{{}}',
            CASE WHEN series.value <= 16000 THEN 0 ELSE 1 END,
            CASE WHEN series.value % 17 = 0 THEN 1 ELSE 0 END,
            0,
            '',
            '',
            '',
            'integration-tests',
            now() - make_interval(secs => series.value),
            now() - make_interval(secs => series.value),
            CASE
                WHEN series.value % 11 = 0 THEN now() + interval '1 day'
                WHEN series.value % 3 = 0 THEN now() - interval '5 minutes'
                ELSE NULL
            END,
            CASE
                WHEN series.value % 13 = 0 THEN now() + interval '1 day'
                WHEN series.value % 7 = 0 THEN now() - interval '2 minutes'
                ELSE NULL
            END
        FROM generate_series(1, 20000) AS series(value);
        """;

    private const string ProcessOutboxClaimSql =
        """
        WITH due AS (
            SELECT o."MessageId"
            FROM "process_outbox_messages" AS o
            WHERE o."Status" = 'Pending'
              AND (o."AvailableAtUtc" IS NULL OR o."AvailableAtUtc" <= now())
            ORDER BY o."CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT 64
        )
        UPDATE "process_outbox_messages" AS o
        SET "Status" = 'Locked',
            "LockId" = 'scenario05',
            "LockedAtUtc" = now(),
            "AttemptCount" = o."AttemptCount" + 1
        FROM due
        WHERE o."MessageId" = due."MessageId"
        RETURNING o."MessageId", o."LockId", o."EventId", o."SubscriberKind";
        """;

    private const string ProcessStepDispatchHeaderSql =
        """
        SELECT s."RunId", s."StepInstanceId", s."Status"
        FROM "process_runtime_steps" AS s
        WHERE s."RunId" = '10000000-0000-0000-0000-000000000005'
          AND (
              s."Status" = 'Ready'
              OR s."Status" = 'WaitingApproval'
              OR s."Status" = 'Running'
          )
          AND s."ActiveClaimToken" IS NULL
        ORDER BY s."AttemptNumber", s."StepInstanceId"
        LIMIT 64;
        """;

    private const string ConnectorCommandClaimSql =
        """
        WITH due AS (
            SELECT c."Id"
            FROM "Workspace_ConnectorCommands" AS c
            WHERE c."Status" = 0
              AND c."ApprovalState" <> 1
              AND (c."NextAttemptAtUtc" IS NULL OR c."NextAttemptAtUtc" <= now())
              AND (c."LeaseExpiresAtUtc" IS NULL OR c."LeaseExpiresAtUtc" <= now())
            ORDER BY COALESCE(c."NextAttemptAtUtc", c."CreatedAtUtc"), c."CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT 64
        )
        UPDATE "Workspace_ConnectorCommands" AS c
        SET "LeaseToken" = 'scenario05',
            "LeaseExpiresAtUtc" = now() + interval '2 minutes',
            "UpdatedAtUtc" = now()
        FROM due
        WHERE c."Id" = due."Id"
        RETURNING c."Id", c."LeaseToken", c."ProjectId", c."ConnectorPluginKey", c."CommandKey";
        """;
}
