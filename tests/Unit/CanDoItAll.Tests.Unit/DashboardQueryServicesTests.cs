using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Hosting;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class DashboardQueryServicesTests
{
    [Fact]
    public async Task Recent_project_activity_is_ordered_bounded_and_not_tracked()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(ProjectsModuleAssemblyMarker).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"recent-project-activity-{Guid.NewGuid():N}")
            .Options;
        var factory = new TrackingDbContextFactory(options);
        var now = DateTimeOffset.Parse("2026-07-22T12:00:00Z");
        var newestId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var firstTieId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondTieId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.Set<Project>().AddRange(
                CreateProject(newestId, "Newest", now, ProjectStatus.Active, "Delivery"),
                CreateProject(secondTieId, "Second tie", now.AddMinutes(-1), ProjectStatus.OnHold, "Review"),
                CreateProject(firstTieId, "First tie", now.AddMinutes(-1), ProjectStatus.Draft, "Plan"),
                CreateProject(Guid.Parse("00000000-0000-0000-0000-000000000004"), "Older 4", now.AddMinutes(-2), ProjectStatus.Completed, "Done"),
                CreateProject(Guid.Parse("00000000-0000-0000-0000-000000000005"), "Older 5", now.AddMinutes(-3), ProjectStatus.Completed, "Done"),
                CreateProject(Guid.Parse("00000000-0000-0000-0000-000000000006"), "Older 6", now.AddMinutes(-4), ProjectStatus.Completed, "Done"),
                CreateProject(Guid.Parse("00000000-0000-0000-0000-000000000007"), "Older 7", now.AddMinutes(-5), ProjectStatus.Completed, "Done"),
                CreateProject(Guid.Parse("00000000-0000-0000-0000-000000000008"), "Older 8", now.AddMinutes(-6), ProjectStatus.Completed, "Done"));
            await dbContext.SaveChangesAsync();
        }

        factory.ResetTrackedEntityCount();
        var service = new RecentProjectActivityQueryService(factory);

        var items = await service.ListAsync(RecentProjectActivityQueryLimits.MaximumItemCount);

        Assert.Equal(
            [
                newestId,
                firstTieId,
                secondTieId,
                Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Guid.Parse("00000000-0000-0000-0000-000000000005"),
                Guid.Parse("00000000-0000-0000-0000-000000000006")
            ],
            items.Select(item => item.Id));
        Assert.Equal(RecentProjectActivityQueryLimits.MaximumItemCount, items.Count);
        Assert.Equal(0, factory.TrackedEntityCount);
        Assert.Equal("Newest", items[0].Name);
        Assert.Equal(ProjectStatus.Active, items[0].Status);
        Assert.Equal("Delivery", items[0].CurrentPhase);
        Assert.Equal(now, items[0].UpdatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RecentProjectActivityQueryLimits.MaximumItemCount + 1)]
    public async Task Recent_project_activity_rejects_an_invalid_item_count(int itemCount)
    {
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"recent-project-activity-validation-{Guid.NewGuid():N}")
            .Options;
        var service = new RecentProjectActivityQueryService(new TrackingDbContextFactory(options));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(itemCount));
    }

    [Fact]
    public async Task Agent_usage_totals_aggregate_the_canonical_provider_rows_once()
    {
        var updatedAtUtc = DateTimeOffset.Parse("2026-07-22T13:00:00Z");
        var projection = new AgentUsageProjection(
            Version: "1.0",
            Revision: 7,
            UpdatedAtUtc: updatedAtUtc,
            Agents: [],
            Providers:
            [
                CreateProviderUsageRow("OpenAI", totalTokens: 17, knownCostUsd: 0.12m, unknownObservationCount: 0),
                CreateProviderUsageRow("Azure OpenAI", totalTokens: 29, knownCostUsd: 0.34m, unknownObservationCount: 2)
            ],
            Models: []);
        var store = new UsageProjectionStoreStub(projection);
        var service = new AgentUsageTotalsQueryService(store);

        var result = await service.GetTotalsAsync();

        Assert.Equal(46L, result.ObservedTokens);
        Assert.Equal(0.46m, result.KnownCostUsd);
        Assert.Equal(2, result.UnknownUsageObservationCount);
        Assert.Equal(updatedAtUtc, result.UpdatedAtUtc);
        Assert.Equal(1, store.LoadUsageProjectionCallCount);
    }

    [Fact]
    public async Task Agent_usage_totals_preserve_unknown_usage_when_tokens_and_cost_are_zero()
    {
        var updatedAtUtc = DateTimeOffset.Parse("2026-07-22T14:00:00Z");
        var projection = new AgentUsageProjection(
            Version: "1.0",
            Revision: 8,
            UpdatedAtUtc: updatedAtUtc,
            Agents: [],
            Providers:
            [
                CreateProviderUsageRow("OpenAI", totalTokens: 0, knownCostUsd: 0m, unknownObservationCount: 3)
            ],
            Models: []);
        var service = new AgentUsageTotalsQueryService(new UsageProjectionStoreStub(projection));

        var result = await service.GetTotalsAsync();

        Assert.Equal(0L, result.ObservedTokens);
        Assert.Equal(0m, result.KnownCostUsd);
        Assert.Equal(3, result.UnknownUsageObservationCount);
        Assert.Equal(updatedAtUtc, result.UpdatedAtUtc);
    }

    [Fact]
    public void Dashboard_query_boundaries_are_registered_with_store_compatible_lifetimes()
    {
        var projectServices = new ServiceCollection();
        var hostingServices = new ServiceCollection();
        var moduleServices = new ServiceCollection();

        projectServices.AddProjectsModule();
        hostingServices.AddAgentFrameworkCore(Path.GetTempPath());
        moduleServices.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        AssertRegistration<IRecentProjectActivityQueryService, RecentProjectActivityQueryService>(
            projectServices,
            ServiceLifetime.Scoped);
        AssertRegistration<IAgentUsageTotalsQueryService, AgentUsageTotalsQueryService>(
            hostingServices,
            ServiceLifetime.Singleton);
        AssertRegistration<IAgentUsageTotalsQueryService, AgentUsageTotalsQueryService>(
            moduleServices,
            ServiceLifetime.Scoped);
    }

    private static Project CreateProject(
        Guid id,
        string name,
        DateTimeOffset updatedAtUtc,
        ProjectStatus status,
        string currentPhase)
    {
        return new Project
        {
            Id = id,
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Status = status,
            CurrentPhase = currentPhase,
            CreatedAtUtc = updatedAtUtc.AddDays(-1),
            UpdatedAtUtc = updatedAtUtc
        };
    }

    private static ProviderUsageProjectionRow CreateProviderUsageRow(
        string providerName,
        int totalTokens,
        decimal knownCostUsd,
        int unknownObservationCount)
    {
        return new ProviderUsageProjectionRow(
            providerName,
            ProviderKind.OpenAi,
            UsageObservationCount: unknownObservationCount + 1,
            KnownUsageObservationCount: 1,
            UnknownUsageObservationCount: unknownObservationCount,
            InputTokens: totalTokens,
            CachedInputTokens: 0,
            OutputTokens: 0,
            ReasoningTokens: 0,
            TotalTokens: totalTokens,
            KnownCostUsd: knownCostUsd,
            FailedRunCount: 0,
            LastUsedAtUtc: null);
    }

    private static void AssertRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService) &&
                descriptor.ImplementationType == typeof(TImplementation) &&
                descriptor.Lifetime == expectedLifetime);
    }

    private sealed class TrackingDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public int TrackedEntityCount { get; private set; }

        public AppDbContext CreateDbContext()
        {
            var dbContext = new AppDbContext(options);
            dbContext.ChangeTracker.Tracked += (_, _) => TrackedEntityCount++;
            return dbContext;
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }

        public void ResetTrackedEntityCount()
        {
            TrackedEntityCount = 0;
        }
    }

    private sealed class UsageProjectionStoreStub : ISandboxWorkspaceStore
    {
        private readonly AgentUsageProjection projection;

        public UsageProjectionStoreStub(AgentUsageProjection projection)
        {
            this.projection = projection;
        }

        public int LoadUsageProjectionCallCount { get; private set; }

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AgentWorkspaceDeletionResult> DeleteAgentWorkspaceDataAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AgentUsageProjection> LoadUsageProjectionAsync(CancellationToken cancellationToken = default)
        {
            LoadUsageProjectionCallCount++;
            return Task.FromResult(projection);
        }

        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
            SandboxWorkspaceCatalog catalog,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
            Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceExecutionSummary> LoadExecutionSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveExecutionAsync(
            SandboxWorkspaceExecutionState executionState,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
            Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceDocument> SaveAsync(
            SandboxWorkspaceDocument document,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
            Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
            Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
