using System.Text.Json;
using CanDoItAll.Migrations.PostgreSql.Migrations;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshJournalFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshCaptureJournalMigrationTests {
    [Fact]
    public async Task Empty_journal_can_be_removed_and_restored_without_changing_existing_runtime_or_receipt_bytes() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await StartAsync(context);
        var before = await ExistingBytesAsync(context);
        var migration = new AddProcessRefreshCaptureJournal();
        await ProcessDecisionMigrationFixture.ApplyAsync(context, true, migration);
        Assert.Equal(before, await ExistingBytesAsync(context));
        await ProcessDecisionMigrationFixture.ApplyAsync(context, false, migration);
        Assert.Equal(before, await ExistingBytesAsync(context));
        Assert.Equal(0, await context.RefreshCaptures.CountAsync());
        Assert.Equal(0, await context.RefreshCapturePayloads.CountAsync());
    }

    [Fact]
    public async Task Downgrade_refuses_reserved_returned_and_settled_evidence_without_losing_payloads_or_receipts() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var store = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        CaptureProbe capturer = new(fixture.Services);
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, capturer, new InterruptedStore(store, Interruption.BeforeBegin))
            .PrepareAsync(state, fixture.CommandContext, admission));
        await using var context = Context(fixture.Services);
        await RefuseAsync(ProcessDecisionRefreshCapturePhase.Reserved, 0);
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, capturer, new InterruptedStore(store, Interruption.AfterReturn))
            .PrepareAsync(state, fixture.CommandContext, admission));
        await RefuseAsync(ProcessDecisionRefreshCapturePhase.Returned, 1);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await Coordinator(fixture.Services, capturer)
            .PrepareAsync(state, fixture.CommandContext, admission)).Status);
        await RefuseAsync(ProcessDecisionRefreshCapturePhase.Settled, 0);
        Assert.Equal(1, capturer.Calls);

        async Task RefuseAsync(ProcessDecisionRefreshCapturePhase phase, int payloadCount) {
            Assert.Equal(phase, (await SavedAsync(fixture.Services, state, admission, ReadBasisMode.RunSnapshot)).Phase);
            var before = await SnapshotAsync(fixture.Services, state);
            var payloads = JsonSerializer.Serialize(await context.RefreshCapturePayloads.AsNoTracking().ToArrayAsync());
            Assert.Equal(payloadCount, await context.RefreshCapturePayloads.CountAsync());
            var refusal = await Assert.ThrowsAsync<PostgresException>(() => ProcessDecisionMigrationFixture.ApplyAsync(context, true, new AddProcessRefreshCaptureJournal()));
            Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
            Assert.Contains("retained Process refresh invocation evidence", refusal.MessageText, StringComparison.Ordinal);
            Assert.Equal(before, await SnapshotAsync(fixture.Services, state));
            Assert.Equal(payloads, JsonSerializer.Serialize(await context.RefreshCapturePayloads.AsNoTracking().ToArrayAsync()));
        }
    }

    [Fact]
    public async Task Database_rejects_invalid_phase_null_pairs_foreign_owners_and_unbounded_payloads() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var store = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, new(fixture.Services), new InterruptedStore(store, Interruption.BeforeBegin))
            .PrepareAsync(state, fixture.CommandContext, admission));
        var before = await SnapshotAsync(fixture.Services, state);
        await using var context = Context(fixture.Services);
        foreach (var assignment in new[] {
            "\"ProtocolVersion\" = 2", "\"OriginalBudgetRevision\" = 0", "\"GrantedWorkTicks\" = 0",
            "\"MaterialGeneration\" = 1", "\"InputFingerprint\" = 'invalid'", "\"MaterialId\" = '00000000-0000-0000-0000-000000000000'",
            "\"Phase\" = 2, \"Revision\" = 2", "\"Revision\" = 2", "\"ConsumedWorkTicks\" = 0",
            "\"Phase\" = 3, \"Revision\" = 3, \"InvocationId\" = gen_random_uuid(), \"Outcome\" = 2",
            "\"Phase\" = 3, \"Revision\" = 2, \"Outcome\" = 7, \"ConsumedWorkTicks\" = 1",
            "\"Phase\" = 3, \"Revision\" = 3, \"InvocationId\" = gen_random_uuid(), \"Outcome\" = 1, \"ConsumedWorkTicks\" = 0",
            "\"Phase\" = 4, \"Revision\" = 3, \"Outcome\" = 7, \"ConsumedWorkTicks\" = 0, \"SettlementCommandId\" = \"AdmissionCommandId\"",
            "\"Phase\" = 4, \"Revision\" = 3, \"Outcome\" = 7, \"ConsumedWorkTicks\" = 0, \"Disposition\" = 1, \"SettlementCommandId\" = \"AdmissionCommandId\""
        }) {
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync($"UPDATE process_decision_refresh_captures SET {assignment}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }
        foreach (var column in new[] { "BudgetId", "ScopeId", "DecisionId", "AdmissionCommandId", "RootRunId", "ReservationCommandId" }) {
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync($"UPDATE process_decision_refresh_captures SET \"{column}\" = gen_random_uuid()"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }
        foreach (var length in new[] { 1, 1_048_577 }) {
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO process_decision_refresh_capture_payloads ("RunId", "ReservationCommandId", "PayloadJson")
                SELECT "RunId", "ReservationCommandId", repeat('x', {length}) FROM process_decision_refresh_captures
                """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }
        var orphan = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            INSERT INTO process_decision_refresh_capture_payloads ("RunId", "ReservationCommandId", "PayloadJson")
            VALUES (gen_random_uuid(), gen_random_uuid(), '[]')
            """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, orphan.SqlState);
        Assert.Equal(before, await SnapshotAsync(fixture.Services, state));
        ProcessRuntimeCommitRequest legacy = new(RuntimeCommandId.New(), state, ProcessRuntimeMutation.Applied(state, [], []));
        Assert.DoesNotContain("RefreshCapture", JsonSerializer.Serialize(legacy), StringComparison.Ordinal);
    }

    private static async Task<string> ExistingBytesAsync(ProcessPersistenceDbContext context)
        => JsonSerializer.Serialize(new {
            States = await context.RuntimeStates.AsNoTracking().OrderBy(row => row.RunId).ToArrayAsync(),
            Generations = await context.DecisionGenerations.AsNoTracking().OrderBy(row => row.DecisionId).ThenBy(row => row.Generation).ToArrayAsync(),
            Receipts = await context.IdempotencyKeys.AsNoTracking().OrderBy(row => row.CommandId).ToArrayAsync()
        });
}
