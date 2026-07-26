using System.Data;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessBlockedRecoveryMigrationIntegrationTests
{
    private const string PreviousMigration =
        "20260725224031_AddProcessStrategyResultUserSafeSummary";

    [Fact]
    public async Task PostgreSql_AppliedSequenceBackfill_UsesResultApplicationOrder()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database =
            PostgresTestDatabaseLease.Create("processblockedrecoverymigration");
        var factory = new WorkflowUsagePostgresDbContextFactory(
            database.CreateAppDbContextOptions());
        var runId = Guid.NewGuid();
        var firstClaimToken = Guid.NewGuid();
        var secondClaimToken = Guid.NewGuid();
        var firstStepId = Guid.NewGuid();
        var secondStepId = Guid.NewGuid();
        var firstResultId = Guid.NewGuid();
        var secondResultId = Guid.NewGuid();
        var createdAtUtc =
            new DateTimeOffset(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

        await using var dbContext = factory.CreateDbContext();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await SeedLegacyResultHistoryAsync(
            dbContext,
            runId,
            firstStepId,
            secondStepId,
            firstClaimToken,
            secondClaimToken,
            firstResultId,
            secondResultId,
            createdAtUtc);

        await migrator.MigrateAsync();

        var firstAppliedSequence =
            await ReadAppliedSequenceAsync(dbContext, runId, firstResultId);
        var secondAppliedSequence =
            await ReadAppliedSequenceAsync(dbContext, runId, secondResultId);
        Assert.Equal(2, firstAppliedSequence);
        Assert.Equal(1, secondAppliedSequence);
    }

    private static async Task SeedLegacyResultHistoryAsync(
        AppDbContext dbContext,
        Guid runId,
        Guid firstStepId,
        Guid secondStepId,
        Guid firstClaimToken,
        Guid secondClaimToken,
        Guid firstResultId,
        Guid secondResultId,
        DateTimeOffset createdAtUtc)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO process_runtime_states
                ("RunId", "RootRunId", "PlanId", "PlanHash", "Status",
                 "UpdatedAtUtc", "ConcurrencyToken")
            VALUES
                ({runId}, {runId}, {Guid.NewGuid()}, {"plan-hash"}, {"Blocked"},
                 {createdAtUtc}, {Guid.NewGuid()});

            INSERT INTO process_dispatch_claims
                ("RunId", "ClaimToken", "StepInstanceId", "OwnerId", "Status",
                 "AttemptNumber", "CreatedAtUtc", "ExpiresAtUtc", "RenewedAtUtc",
                 "ResultIdempotencyKey")
            VALUES
                ({runId}, {firstClaimToken}, {firstStepId}, {"worker-first"},
                 {"Completed"}, {1}, {createdAtUtc}, {createdAtUtc.AddHours(1)},
                 NULL, {firstResultId}),
                ({runId}, {secondClaimToken}, {secondStepId}, {"worker-second"},
                 {"Completed"}, {1}, {createdAtUtc.AddMinutes(1)},
                 {createdAtUtc.AddHours(1)}, NULL, {secondResultId});

            INSERT INTO process_strategy_result_receipts
                ("RunId", "StepInstanceId", "StrategyId", "IdempotencyKey",
                 "Outcome", "AppliedStepStatus", "ResultHash", "DiagnosticsJson",
                 "ProducedArtifactsJson", "RecoveryDecisionJson", "UserSafeSummary")
            VALUES
                ({runId}, {firstStepId}, {"strategy-first"}, {firstResultId},
                 {"NeedsManager"}, {"Blocked"}, {"result-first"}, {"[]"},
                 {"[]"}, NULL, {"First result"}),
                ({runId}, {secondStepId}, {"strategy-second"}, {secondResultId},
                 {"NeedsManager"}, {"Blocked"}, {"result-second"}, {"[]"},
                 {"[]"}, NULL, {"Second result"});

            INSERT INTO process_runtime_events
                ("RootSequence", "EventId", "RootRunId", "RunId", "CorrelationId",
                 "CausationId", "ActorKind", "ActorId", "SchemaVersion",
                 "Sensitivity", "OccurredAtUtc", "EventType", "PayloadHash")
            VALUES
                ({20L}, {Guid.NewGuid()}, {runId}, {runId}, {"first-result"},
                 NULL, {"Agent"}, {"worker-first"}, {"process.runtime.event.v1"},
                 {"Normal"}, {createdAtUtc.AddMinutes(2)},
                 {"DispatchClaimCompleted"}, {firstClaimToken.ToString()}),
                ({21L}, {Guid.NewGuid()}, {runId}, {runId}, {"first-result"},
                 NULL, {"Agent"}, {"worker-first"}, {"process.runtime.event.v1"},
                 {"Normal"}, {createdAtUtc.AddMinutes(2)},
                 {"StepBlocked"}, {"result-first"}),
                ({10L}, {Guid.NewGuid()}, {runId}, {runId}, {"second-result"},
                 NULL, {"Agent"}, {"worker-second"}, {"process.runtime.event.v1"},
                 {"Normal"}, {createdAtUtc.AddMinutes(1)},
                 {"DispatchClaimCompleted"}, {secondClaimToken.ToString()}),
                ({11L}, {Guid.NewGuid()}, {runId}, {runId}, {"second-result"},
                 NULL, {"Agent"}, {"worker-second"}, {"process.runtime.event.v1"},
                 {"Normal"}, {createdAtUtc.AddMinutes(1)},
                 {"StepBlocked"}, {"result-second"});
            """);
    }

    private static async Task<long> ReadAppliedSequenceAsync(
        AppDbContext dbContext,
        Guid runId,
        Guid resultId)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT "AppliedSequence"
            FROM process_strategy_result_receipts
            WHERE "RunId" = @runId
                AND "IdempotencyKey" = @resultId;
            """;
        var runParameter = command.CreateParameter();
        runParameter.ParameterName = "runId";
        runParameter.Value = runId;
        command.Parameters.Add(runParameter);
        var resultParameter = command.CreateParameter();
        resultParameter.ParameterName = "resultId";
        resultParameter.Value = resultId;
        command.Parameters.Add(resultParameter);

        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(value);
    }
}
