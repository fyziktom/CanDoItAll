using System.Text.Json;
using CanDoItAll.Migrations.PostgreSql.Migrations;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshDeliveryQueueTests;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDecisionRefreshDeliveryMigrationTests {
    [Fact]
    public async Task Empty_delivery_migration_preserves_existing_runtime_and_receipt_bytes() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await StartAsync(context);
        var before = await ExistingAsync(context);
        var migration = new AddProcessRefreshDelivery();
        await ProcessDecisionMigrationFixture.ApplyAsync(context, true, migration);
        Assert.Equal(before, await ExistingAsync(context));
        await ProcessDecisionMigrationFixture.ApplyAsync(context, false, migration);
        Assert.Equal(before, await ExistingAsync(context));
        Assert.Equal(0, await context.RefreshDeliveries.CountAsync());
        var index = await context.Database.SqlQueryRaw<string>("""
            SELECT pg_get_indexdef(indexrelid) AS "Value" FROM pg_index
            WHERE indexrelid = '"IX_process_refresh_delivery_due"'::regclass
            """).SingleAsync();
        Assert.Contains("WHERE (\"Disposition\" = 0)", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Downgrade_refuses_every_retained_delivery_including_terminal_outcomes() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await SeedAsync(context);
        await RefuseAsync();
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(TimeSpan.FromMinutes(1)))!;
        await queue.CheckpointAsync(claim, new(ProcessDecisionRefreshDeliveryReason.Advanced));
        await RefuseAsync();

        async Task RefuseAsync() {
            var before = await RowsAsync(context);
            var error = await Assert.ThrowsAsync<PostgresException>(() => ProcessDecisionMigrationFixture.ApplyAsync(context, true, new AddProcessRefreshDelivery()));
            Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
            Assert.Contains("retained Process refresh delivery evidence", error.MessageText, StringComparison.Ordinal);
            Assert.Equal(before, await RowsAsync(context));
        }
    }

    [Fact]
    public async Task Database_rejects_invalid_protocol_lease_disposition_and_foreign_identity() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await SeedAsync(context);
        var before = await RowsAsync(context);
        foreach (var assignment in new[] {
            "\"ProtocolVersion\" = 2", "\"ProgressRevision\" = -1", "\"WakeRevision\" = -1", "\"LeaseGeneration\" = -1",
            "\"LeaseToken\" = gen_random_uuid()", "\"LeaseExpiresAtUtc\" = clock_timestamp()", "\"Disposition\" = 4",
            "\"Disposition\" = 1", "\"Disposition\" = 2", "\"Disposition\" = 3", "\"Reason\" = 13",
            "\"LeaseToken\" = '00000000-0000-0000-0000-000000000000'", "\"RunId\" = '00000000-0000-0000-0000-000000000000'"
        }) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync($"UPDATE process_decision_refresh_delivery SET {assignment}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        }
        foreach (var column in new[] { "RunId", "AdmissionCommandId", "RootRunId" }) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync($"UPDATE process_decision_refresh_delivery SET \"{column}\" = gen_random_uuid()"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        }
        Assert.Equal(before, await RowsAsync(context));
    }

    private static async Task<string> ExistingAsync(ProcessPersistenceDbContext context)
        => JsonSerializer.Serialize(new { States = await context.RuntimeStates.AsNoTracking().OrderBy(row => row.RunId).ToArrayAsync(),
            Receipts = await context.IdempotencyKeys.AsNoTracking().OrderBy(row => row.CommandId).ToArrayAsync() });
}
