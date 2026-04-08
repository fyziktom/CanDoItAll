using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectWorkbenchProjectionMaintenanceIntegrationTests
{
    [Fact]
    public async Task GetStructureAsync_does_not_delete_stale_system_managed_projection_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Read path keeps stale projection rows");
        var staleNodeKey = Guid.NewGuid().ToString("D");
        var rootNodeKey = BuildProjectRootNodeKey(projectId);
        var createdAtUtc = new DateTimeOffset(2026, 4, 5, 13, 0, 0, TimeSpan.Zero);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProjectObjectRecord>().Add(new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = staleNodeKey,
                ObjectType = ProjectObjectType.Connector,
                Title = "Stale projection node",
                Subtitle = "Legacy system-managed row",
                Notes = "Read paths must not delete this row implicitly.",
                ObjectSubtype = "legacy-projection",
                ParentNodeKey = rootNodeKey,
                IsSystemManaged = true,
                PositionX = 540,
                PositionY = 260,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            });
            dbContext.Set<ProjectObjectLinkRecord>().Add(new ProjectObjectLinkRecord
            {
                ProjectId = projectId,
                SourceNodeKey = rootNodeKey,
                TargetNodeKey = staleNodeKey,
                LinkKind = ProjectObjectLinkKind.Contains,
                IsSystemManaged = true,
                CreatedAtUtc = createdAtUtc
            });
            await dbContext.SaveChangesAsync();
        }

        var surface = await workbench.GetStructureAsync(projectId);

        Assert.DoesNotContain(surface.Nodes, node => string.Equals(node.Id, staleNodeKey, StringComparison.Ordinal));
        Assert.DoesNotContain(surface.Links, link => string.Equals(link.TargetId, staleNodeKey, StringComparison.Ordinal));

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await verificationContext.Set<ProjectObjectRecord>()
            .AnyAsync(item => item.ProjectId == projectId && item.NodeKey == staleNodeKey && item.IsSystemManaged));
        Assert.True(await verificationContext.Set<ProjectObjectLinkRecord>()
            .AnyAsync(item => item.ProjectId == projectId && item.TargetNodeKey == staleNodeKey && item.IsSystemManaged));
    }

    [Fact]
    public async Task GetStructureAsync_does_not_delete_stale_projection_layout_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Read path keeps orphan layouts");
        const string staleLayoutNodeKey = "projection:stale-layout";

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProjectStructureProjectionLayoutRecord>().Add(new ProjectStructureProjectionLayoutRecord
            {
                ProjectId = projectId,
                NodeKey = staleLayoutNodeKey,
                PositionX = 810,
                PositionY = 420,
                UpdatedAtUtc = new DateTimeOffset(2026, 4, 5, 13, 15, 0, TimeSpan.Zero)
            });
            await dbContext.SaveChangesAsync();
        }

        var surface = await workbench.GetStructureAsync(projectId);

        Assert.DoesNotContain(surface.Nodes, node => string.Equals(node.Id, staleLayoutNodeKey, StringComparison.Ordinal));

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await verificationContext.Set<ProjectStructureProjectionLayoutRecord>()
            .AnyAsync(item => item.ProjectId == projectId && item.NodeKey == staleLayoutNodeKey));
    }

    [Fact]
    public async Task GetStructureAsync_does_not_write_when_legacy_marker_and_reference_fallback_is_used()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-tests");
        var activeProfile = testEnvironment.CreateManagedSqliteProfile("projection-zero-write");
        await using var services = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: WrapDbContextFactoryWithSaveCounter);
        await using var scope = services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var saveCounter = scope.ServiceProvider.GetRequiredService<SaveChangesCounter>();

        var projectId = await CreateProjectAsync(projects, "Legacy fallback stays zero-write");
        var providerId = Guid.NewGuid();
        var nodeKey = Guid.NewGuid().ToString("D");
        var createdAtUtc = new DateTimeOffset(2026, 4, 5, 13, 30, 0, TimeSpan.Zero);
        var metadataJson = JsonSerializer.Serialize(new
        {
            transcript = new
            {
                transcriptText = "Legacy transcript payload.",
                lastProviderProfileId = providerId,
                lastProviderName = "Offline provider"
            }
        });

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProjectObjectRecord>().Add(new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = nodeKey,
                ObjectType = ProjectObjectType.Transcript,
                Title = "Legacy transcript",
                Subtitle = "Pre-binding row",
                Status = "Review",
                Notes = "Read path should sanitize only the mapped surface.",
                ObjectSubtype = string.Empty,
                ProgressMode = "progress",
                ProgressPercent = 35,
                MarkersJson = """[{"icon":"risk","tone":"danger","label":"Critical"}]""",
                MetadataJson = metadataJson,
                ParentNodeKey = BuildProjectRootNodeKey(projectId),
                PositionX = 610,
                PositionY = 345,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            });
            dbContext.Set<ProjectObjectLinkRecord>().Add(new ProjectObjectLinkRecord
            {
                ProjectId = projectId,
                SourceNodeKey = BuildProjectRootNodeKey(projectId),
                TargetNodeKey = nodeKey,
                LinkKind = ProjectObjectLinkKind.Contains,
                CreatedAtUtc = createdAtUtc
            });
            await dbContext.SaveChangesAsync();
        }

        saveCounter.Reset();

        var surface = await workbench.GetStructureAsync(projectId);
        var normalizedNode = Assert.Single(surface.Nodes, node => node.Id == nodeKey);

        Assert.Equal(0, saveCounter.SaveChangesCount);
        Assert.Equal("risk", normalizedNode.MarkerIcon);
        Assert.Equal("danger", normalizedNode.MarkerTone);
        Assert.Equal("Critical", normalizedNode.MarkerLabel);
        Assert.NotNull(normalizedNode.NodeReferences);
        Assert.Equal(providerId, normalizedNode.NodeReferences!.TranscriptProviderProfileId);
        using (var normalizedMetadataDocument = JsonDocument.Parse(normalizedNode.MetadataJson))
        {
            var transcriptElement = normalizedMetadataDocument.RootElement.GetProperty("transcript");
            Assert.False(transcriptElement.TryGetProperty("lastProviderProfileId", out _));
        }

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var carrier = await verificationContext.Set<ProjectObjectRecord>()
            .SingleAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey);
        Assert.Equal("""[{"icon":"risk","tone":"danger","label":"Critical"}]""", carrier.MarkersJson);
        using (var persistedMetadataDocument = JsonDocument.Parse(carrier.MetadataJson))
        {
            var transcriptElement = persistedMetadataDocument.RootElement.GetProperty("transcript");
            Assert.True(transcriptElement.TryGetProperty("lastProviderProfileId", out _));
        }

        Assert.False(await verificationContext.Set<ProjectNodeBindingRecord>()
            .AnyAsync(item => item.ProjectObjectId == carrier.Id));
        Assert.False(await verificationContext.Set<ProjectNodeReferenceRecord>()
            .AnyAsync(item => item.ProjectObjectId == carrier.Id));
    }

    [Fact]
    public async Task Explicit_projection_repair_removes_stale_system_managed_rows_and_orphan_layouts_idempotently()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var maintenance = scope.ServiceProvider.GetRequiredService<ProjectStructureProjectionMaintenanceService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Explicit projection repair");
        var staleNodeKey = Guid.NewGuid().ToString("D");
        const string staleLayoutNodeKey = "projection:repair-me";
        var rootNodeKey = BuildProjectRootNodeKey(projectId);
        var createdAtUtc = new DateTimeOffset(2026, 4, 5, 13, 45, 0, TimeSpan.Zero);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProjectObjectRecord>().Add(new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = staleNodeKey,
                ObjectType = ProjectObjectType.Connector,
                Title = "Stale projection node",
                Subtitle = "Legacy system-managed row",
                Notes = "Repair should remove this row explicitly.",
                ObjectSubtype = "legacy-projection",
                ParentNodeKey = rootNodeKey,
                IsSystemManaged = true,
                PositionX = 540,
                PositionY = 260,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            });
            dbContext.Set<ProjectObjectLinkRecord>().Add(new ProjectObjectLinkRecord
            {
                ProjectId = projectId,
                SourceNodeKey = rootNodeKey,
                TargetNodeKey = staleNodeKey,
                LinkKind = ProjectObjectLinkKind.Contains,
                IsSystemManaged = true,
                CreatedAtUtc = createdAtUtc
            });
            dbContext.Set<ProjectStructureProjectionLayoutRecord>().Add(new ProjectStructureProjectionLayoutRecord
            {
                ProjectId = projectId,
                NodeKey = staleLayoutNodeKey,
                PositionX = 820,
                PositionY = 460,
                UpdatedAtUtc = createdAtUtc
            });
            await dbContext.SaveChangesAsync();
        }

        var firstRepair = await maintenance.RepairAsync(projectId);

        Assert.Equal(1, firstRepair.RemovedSystemManagedNodeCount);
        Assert.Equal(1, firstRepair.RemovedSystemManagedLinkCount);
        Assert.Equal(1, firstRepair.RemovedOrphanLayoutCount);
        Assert.Equal(3, firstRepair.TotalRemovedCount);

        await using (var verificationContext = await dbContextFactory.CreateDbContextAsync())
        {
            Assert.False(await verificationContext.Set<ProjectObjectRecord>()
                .AnyAsync(item => item.ProjectId == projectId && item.IsSystemManaged));
            Assert.False(await verificationContext.Set<ProjectObjectLinkRecord>()
                .AnyAsync(item => item.ProjectId == projectId && item.IsSystemManaged));
            Assert.False(await verificationContext.Set<ProjectStructureProjectionLayoutRecord>()
                .AnyAsync(item => item.ProjectId == projectId && item.NodeKey == staleLayoutNodeKey));
        }

        var secondRepair = await maintenance.RepairAsync(projectId);

        Assert.Equal(0, secondRepair.RemovedSystemManagedNodeCount);
        Assert.Equal(0, secondRepair.RemovedSystemManagedLinkCount);
        Assert.Equal(0, secondRepair.RemovedOrphanLayoutCount);
        Assert.Equal(0, secondRepair.TotalRemovedCount);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
    {
        return $"project:{projectId}";
    }

    private static void WrapDbContextFactoryWithSaveCounter(IServiceCollection services)
    {
        services.AddSingleton<SaveChangesCounter>();

        var factoryDescriptor = services.Last(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<AppDbContext>));
        services.Remove(factoryDescriptor);
        services.Add(new ServiceDescriptor(
            typeof(IDbContextFactory<AppDbContext>),
            serviceProvider =>
            {
                var innerFactory = (IDbContextFactory<AppDbContext>)CreateService(serviceProvider, factoryDescriptor);
                var counter = serviceProvider.GetRequiredService<SaveChangesCounter>();
                return new CountingDbContextFactory(innerFactory, counter);
            },
            factoryDescriptor.Lifetime));
    }

    private static object CreateService(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException($"Service descriptor for '{descriptor.ServiceType}' does not expose an implementation.");
    }

    private sealed class CountingDbContextFactory(
        IDbContextFactory<AppDbContext> innerFactory,
        SaveChangesCounter counter) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            var dbContext = innerFactory.CreateDbContext();
            AttachSaveCounter(dbContext);
            return dbContext;
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var dbContext = await innerFactory.CreateDbContextAsync(cancellationToken);
            AttachSaveCounter(dbContext);
            return dbContext;
        }

        private void AttachSaveCounter(AppDbContext dbContext)
        {
            dbContext.SavedChanges += (_, _) => counter.Increment();
        }
    }

    private sealed class SaveChangesCounter
    {
        private int saveChangesCount;

        public int SaveChangesCount => saveChangesCount;

        public void Increment()
        {
            Interlocked.Increment(ref saveChangesCount);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref saveChangesCount, 0);
        }
    }
}
