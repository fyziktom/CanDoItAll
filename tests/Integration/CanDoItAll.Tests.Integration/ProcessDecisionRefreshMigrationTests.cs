using System.Text.Json;
using CanDoItAll.Migrations.PostgreSql.Migrations;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshMigrationTests {
    [Fact]
    public async Task Upgrade_retains_legacy_generation_and_receipt_bytes_and_downgrade_without_refresh_evidence_is_safe() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await StartAsync(context);
        var receipts = JsonSerializer.Serialize(await context.IdempotencyKeys.AsNoTracking().OrderBy(row => row.CommandId).ToArrayAsync());
        var original = await context.Database.SqlQueryRaw<string>(
            "SELECT (to_jsonb(g) - 'RefreshCommandId')::text AS \"Value\" FROM process_decision_generations g").SingleAsync();
        var migration = new AddProcessRefreshAdmission();
        await ProcessDecisionMigrationFixture.ApplyAsync(context, true, migration);
        Assert.Equal(original, await context.Database.SqlQueryRaw<string>(
            "SELECT to_jsonb(g)::text AS \"Value\" FROM process_decision_generations g").SingleAsync());
        await ProcessDecisionMigrationFixture.ApplyAsync(context, false, migration);
        Assert.Null((await context.DecisionGenerations.AsNoTracking().SingleAsync()).RefreshCommandId);
        Assert.Equal(original, await context.Database.SqlQueryRaw<string>(
            "SELECT (to_jsonb(g) - 'RefreshCommandId')::text AS \"Value\" FROM process_decision_generations g").SingleAsync());
        Assert.Equal(receipts, JsonSerializer.Serialize(await context.IdempotencyKeys.AsNoTracking().OrderBy(row => row.CommandId).ToArrayAsync()));
    }

    [Fact]
    public async Task Downgrade_refuses_retained_admission_or_receipt_without_losing_the_column_or_evidence() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var review = fixture.Request;
        ProcessDecisionRefreshRequest request = new(new(Guid.NewGuid()), review.Child, review.StrategyId, review.WaitingResultKey,
            review.Decision, review.Budget, null, ProcessDecisionRefreshKind.ManagerRefresh);
        await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>().AdmitAsync(fixture.State, fixture.CommandContext, request);
        await using var context = Context(fixture.Services);
        var receipts = JsonSerializer.Serialize(await context.IdempotencyKeys.AsNoTracking().OrderBy(row => row.CommandId).ToArrayAsync());
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => ProcessDecisionMigrationFixture.ApplyAsync(context, true, new AddProcessRefreshAdmission()));
        Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
        Assert.Contains("retained Process refresh admission", refusal.MessageText, StringComparison.Ordinal);
        Assert.Equal(request.CommandId.Value, (await context.DecisionGenerations.AsNoTracking().SingleAsync()).RefreshCommandId);
        await context.Database.ExecuteSqlRawAsync("UPDATE process_decision_generations SET \"RefreshCommandId\" = NULL");
        refusal = await Assert.ThrowsAsync<PostgresException>(() => ProcessDecisionMigrationFixture.ApplyAsync(context, true, new AddProcessRefreshAdmission()));
        Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
        Assert.Null((await context.DecisionGenerations.AsNoTracking().SingleAsync()).RefreshCommandId);
        Assert.Equal(receipts, JsonSerializer.Serialize(await context.IdempotencyKeys.AsNoTracking().OrderBy(row => row.CommandId).ToArrayAsync()));
    }
}
