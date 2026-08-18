using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowProjectStructureReportPersistenceIntegrationTests
{
    private static readonly DateTimeOffset UtcMidnight =
        new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkflowId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid VersionId =
        Guid.Parse("a2000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task PostgreSqlGroupsProjectWorkflowExpensesByUtcActivityDate()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowprojectreport");
        var options = database.CreateAppDbContextOptions();
        await using (var dbContext = new AppDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var projectId = Guid.NewGuid();
        var previousDayRun = CreateRun(projectId, UtcMidnight.AddMinutes(-1));
        var firstCurrentDayRun = CreateRun(projectId, UtcMidnight.AddMinutes(1));
        var secondCurrentDayRun = CreateRun(projectId, UtcMidnight.AddMinutes(10));

        await using (var dbContext = new AppDbContext(options))
        {
            dbContext.Set<WorkflowRunRecordEntity>().AddRange(
                previousDayRun,
                firstCurrentDayRun,
                secondCurrentDayRun);
            dbContext.Set<WorkflowUsageObservationRecordEntity>().AddRange(
                CreateUsage(previousDayRun, 1.25m),
                CreateUsage(firstCurrentDayRun, 2.50m),
                CreateUsage(secondCurrentDayRun, 0.75m));
            await dbContext.SaveChangesAsync();
        }

        var store = new PersistentWorkflowRunStore(new TestDbContextFactory(options));
        var report = await store.QueryProjectStructureReportAsync(
            new WorkflowProjectStructureReportQuery(
                [projectId],
                UtcMidnight.AddHours(-1),
                UtcMidnight.AddHours(1),
                UtcMidnight.AddHours(-1),
                [WorkflowRunState.Completed],
                pageSize: 10));

        Assert.Equal(3, report.TotalCount);
        Assert.Equal(4.50m, report.KnownCostUsd);
        Assert.Equal(
            [
                new WorkflowProjectStructureDailyCost(
                    DateOnly.FromDateTime(UtcMidnight.AddDays(-1).UtcDateTime),
                    1.25m),
                new WorkflowProjectStructureDailyCost(
                    DateOnly.FromDateTime(UtcMidnight.UtcDateTime),
                    3.25m)
            ],
            report.DailyCost);
    }

    private static WorkflowRunRecordEntity CreateRun(
        Guid projectId,
        DateTimeOffset completedAtUtc)
        => new()
        {
            RunId = Guid.NewGuid(),
            WorkflowId = WorkflowId,
            VersionId = VersionId,
            State = WorkflowRunState.Completed,
            Backend = WorkflowRuntimeBackendKind.InProcess,
            BackendRunId = $"postgres-project-report-{Guid.NewGuid():N}",
            Summary = "Completed project workflow.",
            CreatedAtUtc = completedAtUtc.AddMinutes(-5),
            UpdatedAtUtc = completedAtUtc,
            TerminalAtUtc = completedAtUtc,
            OriginJson = string.Empty,
            OriginKind = WorkflowLaunchOriginKind.ProjectStructureNode,
            OriginProjectId = projectId
        };

    private static WorkflowUsageObservationRecordEntity CreateUsage(
        WorkflowRunRecordEntity run,
        decimal costUsd)
        => new()
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            WorkflowId = run.WorkflowId,
            VersionId = run.VersionId,
            NodeId = "postgres-project-report-node",
            ProducerKind = WorkflowUsageProducerKind.LlmComponent,
            InvocationId = Guid.NewGuid(),
            Attempt = 1,
            ProviderName = "Integration test provider",
            ProviderNameKey = "INTEGRATION TEST PROVIDER",
            Model = "integration-test-model",
            ModelKey = "INTEGRATION-TEST-MODEL",
            SourcePhase = "project-report",
            UsageStatus = WorkflowUsageStatus.Observed,
            PricingStatus = WorkflowPricingStatus.Known,
            PricingProvenance = WorkflowUsagePricingProvenance.PricingProfileSnapshot,
            CostUsd = costUsd,
            PricingProfileHash = "integration-test",
            PricingVersion = "v1",
            ProviderRequestId = string.Empty,
            ProviderResponseId = string.Empty,
            RecordedAtUtc = run.ReportingActivityAtUtc,
            OriginJson = string.Empty
        };

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
