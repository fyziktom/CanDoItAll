using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PostgreSqlClaimQueryPlanIntegrationTests {
    private const string ProcessOutboxClaimIndex = "IX_Processes_Outbox_PendingClaimOrder";
    private const string ProcessStepDispatchHeaderIndex = "IX_Processes_StepRuns_ProcessRunId_Sequence";
    private const string AutomationDeliveryClaimIndex = "IX_Automation_EnvelopeDeliveries_DueClaimOrder";
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
        AssertPlanUsesIndex(processOutboxPlan, ProcessOutboxClaimIndex, "Processes_Outbox");

        var processStepDispatchPlan = await ExplainAsync(dbContext, ProcessStepDispatchHeaderSql);
        await WritePlanAsync("process-step-dispatch-header", processStepDispatchPlan);
        AssertPlanUsesIndex(processStepDispatchPlan, ProcessStepDispatchHeaderIndex, "Processes_StepRuns");

        var automationDeliveryPlan = await ExplainAsync(dbContext, AutomationDeliveryClaimSql);
        await WritePlanAsync("automation-delivery-claim", automationDeliveryPlan);
        AssertPlanUsesIndex(automationDeliveryPlan, AutomationDeliveryClaimIndex, "Automation_EnvelopeDeliveries");

        var connectorCommandPlan = await ExplainAsync(dbContext, ConnectorCommandClaimSql);
        await WritePlanAsync("connector-command-claim", connectorCommandPlan);
        AssertPlanUsesIndex(connectorCommandPlan, ConnectorCommandClaimIndex, "Workspace_ConnectorCommands");
    }

    private static async Task AssertIndexesExistAsync(AppDbContext dbContext) {
        var expected = new HashSet<string>(
            [
                ProcessOutboxClaimIndex,
                ProcessStepDispatchHeaderIndex,
                AutomationDeliveryClaimIndex,
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
                  'IX_Processes_Outbox_PendingClaimOrder',
                'IX_Processes_StepRuns_ProcessRunId_Sequence',
                  'IX_Automation_EnvelopeDeliveries_DueClaimOrder',
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
        await dbContext.Database.ExecuteSqlRawAsync(SeedAutomationDeliveriesSql);
        await dbContext.Database.ExecuteSqlRawAsync(SeedConnectorCommandsSql);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ANALYZE "Processes_Outbox";
            ANALYZE "Processes_StepRuns";
            ANALYZE "Automation_EnvelopeDeliveries";
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
        var directory = Environment.GetEnvironmentVariable("CANDOITALL_SB05_QUERY_PLAN_DIR");
        if (string.IsNullOrWhiteSpace(directory)) {
            return;
        }

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"{planName}.txt"), plan);
    }

    private static void AssertPlanUsesIndex(string plan, string indexName, string tableName) {
        Assert.Contains(indexName, plan, StringComparison.Ordinal);
        Assert.DoesNotContain($"Seq Scan on \"{tableName}\"", plan, StringComparison.Ordinal);
    }

    private const string SeedProcessOutboxSql =
        """
        INSERT INTO "Processes_Outbox" (
            "Id",
            "ProjectId",
            "ProcessDefinitionId",
            "ProcessRunId",
            "CommandKey",
            "PayloadJson",
            "Status",
            "AttemptCount",
            "LastError",
            "LeaseToken",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "NextAttemptAtUtc",
            "LeaseExpiresAtUtc")
        SELECT
            ('00000000-0000-0000-1000-' || lpad(series.value::text, 12, '0'))::uuid,
            NULL,
            NULL,
            NULL,
            CASE WHEN series.value % 5 = 0 THEN 'dispatch-run-automation' ELSE 'search-upsert' END,
            '{{}}',
            CASE WHEN series.value <= 16000 THEN 0 ELSE 1 END,
            0,
            '',
            '',
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

    private const string SeedProcessStepDispatchSql =
        """
        INSERT INTO "Processes_Definitions" (
            "Id",
            "Name",
            "Slug",
            "Summary",
            "ValueStatement",
            "CustomerName",
            "OwnerName",
            "InterfaceContractSummary",
            "GovernanceNotes",
            "Criticality",
            "AutonomyLevel",
            "Status",
            "NextVersionNumber",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "ConcurrencyToken")
        VALUES (
            '10000000-0000-0000-0000-000000000001',
            'SB05 claim plan',
            'sb05-claim-plan',
            '',
            '',
            '',
            '',
            '',
            '',
            'Standard',
            'Assisted',
            'Published',
            2,
            now(),
            now(),
            '10000000-0000-0000-0000-000000000003');

        INSERT INTO "Processes_DefinitionVersions" (
            "Id",
            "ProcessDefinitionId",
            "VersionNumber",
            "Status",
            "ChangeSummary",
            "GovernancePolicySummary",
            "ConstitutionRuleSummary",
            "OperatingModeSummary",
            "SimulationReadinessSummary",
            "ManagerAgentOverrideName",
            "ImportedFrom",
            "ImportWarnings",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "PublishedAtUtc",
            "PublishedBy",
            "ConcurrencyToken")
        VALUES (
            '10000000-0000-0000-0000-000000000002',
            '10000000-0000-0000-0000-000000000001',
            1,
            'Published',
            '',
            '',
            '',
            '',
            '',
            '',
            '',
            '',
            now(),
            now(),
            now(),
            'integration-tests',
            '10000000-0000-0000-0000-000000000004');

        INSERT INTO "Processes_StepDefinitions" (
            "Id",
            "ProcessDefinitionVersionId",
            "Key",
            "Title",
            "Subtitle",
            "Notes",
            "StepKind",
            "SubprocessDefinitionSnapshotName",
            "AllowsManualSkip",
            "AllowsSafeRefusal",
            "RequiresApproval",
            "RequiresDecisionRecord",
            "InputContractSummary",
            "OutputContractSummary",
            "EvidenceContractSummary",
            "DecisionRightsSummary",
            "ExceptionPolicySummary",
            "TargetLeadHours",
            "OrderIndex",
            "CanvasX",
            "CanvasY",
            "BranchCanvasX",
            "BranchCanvasY")
        SELECT
            ('00000000-0000-0000-4000-' || lpad(series.value::text, 12, '0'))::uuid,
            '10000000-0000-0000-0000-000000000002',
            'step-' || series.value,
            'Step ' || series.value,
            '',
            '',
            'Work',
            '',
            false,
            false,
            false,
            false,
            '',
            '',
            '',
            '',
            '',
            1,
            series.value,
            0,
            0,
            0,
            0
        FROM generate_series(1, 6000) AS series(value);

        INSERT INTO "Processes_Runs" (
            "Id",
            "ProcessDefinitionId",
            "ProcessDefinitionVersionId",
            "HierarchyDepth",
            "Name",
            "Status",
            "OperatingMode",
            "TriggerReason",
            "GovernanceSnapshot",
            "PolicySnapshot",
            "ExecutorSnapshotSummary",
            "ManagerAgentName",
            "ReplayPackageKey",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "StartedAtUtc",
            "EstimatedCost",
            "ActualCost",
            "FirstTimeRightPercent",
            "SlaAttainmentPercent",
            "ConcurrencyToken")
        VALUES (
            '10000000-0000-0000-0000-000000000005',
            '10000000-0000-0000-0000-000000000001',
            '10000000-0000-0000-0000-000000000002',
            0,
            'SB05 claim plan run',
            'Active',
            'AssistedExecution',
            '',
            '',
            '',
            '',
            '',
            '',
            now(),
            now(),
            now(),
            0,
            0,
            100,
            100,
            '10000000-0000-0000-0000-000000000006');

        INSERT INTO "Processes_StepRuns" (
            "Id",
            "ProcessRunId",
            "StepDefinitionId",
            "Sequence",
            "Title",
            "StepKind",
            "Status",
            "RoleSnapshotSummary",
            "CurrentExecutorName",
            "DecisionSummary",
            "BlockedReason",
            "RefusalReason",
            "ExceptionSummary",
            "InputQualitySummary",
            "SelectedBranchOutcomeTitle",
            "WaitMinutes",
            "TouchMinutes",
            "BlockedMinutes",
            "ReworkCount",
            "CapabilityGapSeverity",
            "AutomationDispatchClaimToken",
            "AutomationDispatchClaimedBy",
            "AutomationDispatchLeaseExpiresAtUtc",
            "AutomationDispatchAttemptCount",
            "ConcurrencyToken")
        SELECT
            ('00000000-0000-0000-5000-' || lpad(series.value::text, 12, '0'))::uuid,
            '10000000-0000-0000-0000-000000000005',
            ('00000000-0000-0000-4000-' || lpad(series.value::text, 12, '0'))::uuid,
            series.value,
            'Step ' || series.value,
            'Work',
            CASE
                WHEN series.value % 7 = 0 THEN 'Completed'
                WHEN series.value % 5 = 0 THEN 'InProgress'
                WHEN series.value % 3 = 0 THEN 'WaitingApproval'
                ELSE 'Ready'
            END,
            '',
            '',
            '',
            '',
            '',
            '',
            '',
            '',
            0,
            0,
            0,
            0,
            'None',
            '',
            '',
            CASE
                WHEN series.value % 13 = 0 THEN now() + interval '1 day'
                WHEN series.value % 11 = 0 THEN now() - interval '2 minutes'
                ELSE NULL
            END,
            0,
            ('00000000-0000-0000-6000-' || lpad(series.value::text, 12, '0'))::uuid
        FROM generate_series(1, 6000) AS series(value);
        """;

    private const string SeedAutomationDeliveriesSql =
        """
        INSERT INTO "Automation_Envelopes" (
            "Id",
            "EnvelopeType",
            "PayloadJson",
            "State",
            "AttemptCount",
            "AvailableAtUtc",
            "CreatedAtUtc",
            "UpdatedAtUtc")
        SELECT
            ('00000000-0000-0000-2000-' || lpad(series.value::text, 12, '0'))::uuid,
            'sb05.claim-plan',
            '{{}}',
            0,
            0,
            now(),
            now() - make_interval(secs => series.value),
            now()
        FROM generate_series(1, 20000) AS series(value);

        INSERT INTO "Automation_EnvelopeDeliveries" (
            "Id",
            "EnvelopeId",
            "EnvelopeType",
            "HandlerKey",
            "State",
            "AttemptCount",
            "MaxAttempts",
            "AvailableAtUtc",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "LastError",
            "LockToken",
            "LockedAtUtc")
        SELECT
            ('00000000-0000-0000-3000-' || lpad(series.value::text, 12, '0'))::uuid,
            ('00000000-0000-0000-2000-' || lpad(series.value::text, 12, '0'))::uuid,
            'sb05.claim-plan',
            'handler',
            CASE
                WHEN series.value % 17 = 0 THEN 3
                WHEN series.value % 7 = 0 THEN 1
                WHEN series.value % 5 = 0 THEN 2
                ELSE 0
            END,
            0,
            3,
            CASE WHEN series.value % 19 = 0 THEN now() + interval '1 day' ELSE now() - interval '5 minutes' END,
            now() - make_interval(secs => series.value),
            now(),
            '',
            '',
            CASE
                WHEN series.value % 7 = 0 THEN now() - interval '10 minutes'
                ELSE NULL
            END
        FROM generate_series(1, 20000) AS series(value);
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
            'sb05-plugin',
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
            SELECT o."Id"
            FROM "Processes_Outbox" AS o
            WHERE o."Status" = 0
              AND (o."NextAttemptAtUtc" IS NULL OR o."NextAttemptAtUtc" <= now())
              AND (o."LeaseExpiresAtUtc" IS NULL OR o."LeaseExpiresAtUtc" <= now())
            ORDER BY COALESCE(o."NextAttemptAtUtc", o."CreatedAtUtc"), o."CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT 64
        )
        UPDATE "Processes_Outbox" AS o
        SET "LeaseToken" = 'sb05',
            "LeaseExpiresAtUtc" = now() + interval '2 minutes',
            "UpdatedAtUtc" = now()
        FROM due
        WHERE o."Id" = due."Id"
        RETURNING o."Id", o."LeaseToken", o."ProcessRunId", o."CommandKey";
        """;

    private const string ProcessStepDispatchHeaderSql =
        """
        SELECT s."Id", s."Status"
        FROM "Processes_StepRuns" AS s
        WHERE s."ProcessRunId" = '10000000-0000-0000-0000-000000000005'
          AND (
              s."Status" = 'Ready'
              OR s."Status" = 'WaitingApproval'
              OR s."Status" = 'InProgress'
          )
          AND (s."AutomationDispatchLeaseExpiresAtUtc" IS NULL OR s."AutomationDispatchLeaseExpiresAtUtc" <= now())
        ORDER BY s."Sequence"
        LIMIT 64;
        """;

    private const string AutomationDeliveryClaimSql =
        """
        WITH due AS (
            SELECT d."Id"
            FROM "Automation_EnvelopeDeliveries" AS d
            WHERE d."AvailableAtUtc" <= now()
              AND (
                  d."State" = 0
                  OR d."State" = 2
                  OR (
                      d."State" = 1
                      AND d."LockedAtUtc" IS NOT NULL
                      AND d."LockedAtUtc" <= now() - interval '2 minutes'
                  )
              )
            ORDER BY d."AvailableAtUtc", d."CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT 64
        )
        UPDATE "Automation_EnvelopeDeliveries" AS d
        SET "State" = 1,
            "AttemptCount" = d."AttemptCount" + 1,
            "LastAttemptAtUtc" = now(),
            "UpdatedAtUtc" = now(),
            "CompletedAtUtc" = NULL,
            "LockedAtUtc" = now(),
            "LockToken" = 'sb05'
        FROM due
        WHERE d."Id" = due."Id"
        RETURNING d."Id", d."LockToken", d."EnvelopeId";
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
        SET "LeaseToken" = 'sb05',
            "LeaseExpiresAtUtc" = now() + interval '2 minutes',
            "UpdatedAtUtc" = now()
        FROM due
        WHERE c."Id" = due."Id"
        RETURNING c."Id", c."LeaseToken", c."ProjectId", c."ConnectorPluginKey", c."CommandKey";
        """;
}
