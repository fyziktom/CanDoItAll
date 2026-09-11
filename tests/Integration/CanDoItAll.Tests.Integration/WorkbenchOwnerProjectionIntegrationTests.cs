using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkbenchOwnerProjectionIntegrationTests {
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public async Task Owner_projections_preserve_fields_order_bindings_layout_and_plan_timestamps() {
        var reads = new OwnerReadProbe();
        await using var application = await CreateObservedApplicationAsync(reads);
        await using var scope = application.Services.CreateAsyncScope();
        var project = new Project { Name = "Projection owner" };
        var foreignProject = new Project { Name = "Foreign projection owner" };
        var link = CreateResource(project.Id, "A legacy link");
        var webhook = CreateResource(project.Id, "B webhook", WebhookResourceConnectorPlugin.PluginKey);
        var hidden = CreateResource(project.Id, "C hidden");
        var newerPlan = CreatePlan(project.Id, "Newer plan", CreatedAt.AddHours(2));
        var olderPlan = CreatePlan(project.Id, "Older plan", CreatedAt.AddHours(1));
        await using (var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync()) {
            schema.AddRange(project, foreignProject, link, webhook, hidden, newerPlan, olderPlan,
                CreateResource(foreignProject.Id, "0 Foreign resource"),
                CreatePlan(foreignProject.Id, "Foreign plan", CreatedAt.AddHours(3)),
                CreatePlan(null, "Unassigned plan", CreatedAt.AddHours(4)));
            await schema.SaveChangesAsync();
        }

        var assembledAt = CreatedAt.AddDays(1);
        var layoutAt = assembledAt.AddMinutes(1);
        var layouts = new Dictionary<string, ProjectStructureProjectionLayoutRecord>(StringComparer.Ordinal) {
            [$"resource:{link.Id}"] = new() {
                ProjectId = project.Id,
                NodeKey = $"resource:{link.Id}",
                PositionX = 91,
                PositionY = 92,
                UpdatedAtUtc = layoutAt
            },
            [$"resource:{hidden.Id}"] = new() {
                ProjectId = project.Id,
                NodeKey = $"resource:{hidden.Id}",
                IsHidden = true,
                UpdatedAtUtc = layoutAt
            }
        };
        var context = new ProjectStructureProjectionContext(project.Id, assembledAt, layouts);
        var contributors = scope.ServiceProvider.GetServices<IProjectStructureProjectionContributor>().ToArray();
        var resources = Assert.Single(contributors.OfType<ProjectResourceProjectionContributor>());
        var testPlans = Assert.Single(contributors.OfType<TestPlanProjectionContributor>());
        reads.Reset();
        await resources.ContributeAsync(context, CancellationToken.None);
        await testPlans.ContributeAsync(context, CancellationToken.None);

        Assert.Equal(1, reads.ResourceQueries);
        Assert.Equal(1, reads.TestPlanQueries);
        Assert.Equal([$"resource:{link.Id}", $"resource:{webhook.Id}", $"test-plan:{newerPlan.Id}", $"test-plan:{olderPlan.Id}"],
            context.Nodes.Select(node => node.NodeKey));
        Assert.All(context.Nodes, node => {
            Assert.Equal(project.Id, node.ProjectId);
            Assert.Equal($"project:{project.Id}", node.ParentNodeKey);
            Assert.True(node.IsSystemManaged);
            Assert.Equal(CreatedAt, node.CreatedAtUtc);
        });
        var linkNode = context.Nodes[0];
        Assert.Equal(ProjectObjectType.Link, linkNode.ObjectType);
        Assert.Empty(linkNode.ObjectSubtype);
        Assert.Equal(link.Name, linkNode.Title);
        Assert.Equal(link.LocationOrIdentifier, linkNode.Subtitle);
        Assert.Equal(ResourceValidationStatus.Warning.ToString(), linkNode.Status);
        Assert.Equal(link.Description, linkNode.Notes);
        Assert.Equal((91d, 92d), (linkNode.PositionX, linkNode.PositionY));
        Assert.Equal(layoutAt, linkNode.UpdatedAtUtc);
        AssertBinding(linkNode, $"/resources?resourceId={link.Id}", "resource", link.Id);

        var webhookNode = context.Nodes[1];
        Assert.Equal(ProjectObjectType.Connector, webhookNode.ObjectType);
        Assert.Equal("webhook-endpoint", webhookNode.ObjectSubtype);
        Assert.Equal((760d, 220d), (webhookNode.PositionX, webhookNode.PositionY));
        Assert.Equal(assembledAt, webhookNode.UpdatedAtUtc);
        AssertBinding(webhookNode, $"/resources?resourceId={webhook.Id}", "resource", webhook.Id);

        foreach (var (plan, index) in new[] { newerPlan, olderPlan }.Select((plan, index) => (plan, index))) {
            var node = context.Nodes[index + 2];
            Assert.Equal(ProjectObjectType.TestPlan, node.ObjectType);
            Assert.Equal(plan.Title, node.Title);
            Assert.Equal(plan.Phase, node.Subtitle);
            Assert.Equal("Planned", node.Status);
            Assert.Equal(plan.CoverageGoal, node.Notes);
            Assert.Equal((1100d, 620d + index * 140d), (node.PositionX, node.PositionY));
            Assert.Equal(plan.UpdatedAtUtc, node.StartUtc);
            Assert.Equal(plan.UpdatedAtUtc.AddHours(1), node.EndUtc);
            Assert.Equal(3600, node.DurationSeconds);
            Assert.Equal(assembledAt, node.UpdatedAtUtc);
            AssertBinding(node, $"/test-lab?planId={plan.Id}", "test-plan", plan.Id);
        }
        Assert.All(context.Links, linkRecord => {
            Assert.Equal(project.Id, linkRecord.ProjectId);
            Assert.Equal($"project:{project.Id}", linkRecord.SourceNodeKey);
            Assert.True(linkRecord.IsSystemManaged);
            Assert.Equal(assembledAt, linkRecord.CreatedAtUtc);
        });
        Assert.Equal(3, context.Links.Count(item => item.LinkKind == ProjectObjectLinkKind.Uses));
        Assert.Equal(2, context.Links.Count(item => item.LinkKind == ProjectObjectLinkKind.Tests));
        Assert.Equal([$"resource:{link.Id}", $"resource:{webhook.Id}", $"resource:{hidden.Id}",
            $"test-plan:{newerPlan.Id}", $"test-plan:{olderPlan.Id}"], context.Links.Select(item => item.TargetNodeKey));
    }

    [Fact]
    public async Task Owner_scope_facts_deny_foreign_projects_and_preserve_missing_and_canonical_precedence() {
        var reads = new OwnerReadProbe();
        await using var application = await CreateObservedApplicationAsync(reads);
        await using var scope = application.Services.CreateAsyncScope();
        var project = new Project { Name = "Scope owner" };
        var foreignProject = new Project { Name = "Other scope owner" };
        var resource = CreateResource(project.Id, "Scoped webhook", WebhookResourceConnectorPlugin.PluginKey);
        var legacy = CreateResource(project.Id, "Scoped legacy link");
        var plan = CreatePlan(project.Id, "Scoped plan", CreatedAt);
        var unassigned = CreatePlan(null, "Unassigned plan", CreatedAt);
        var shadowed = CreateResource(foreignProject.Id, "Canonical shadow");
        var canonical = new ProjectObjectRecord {
            ProjectId = project.Id,
            NodeKey = $"resource:{shadowed.Id}",
            ObjectType = ProjectObjectType.ProjectBlock,
            ObjectSubtype = "architecture",
            Title = "Canonical node wins",
            CreatedAtUtc = CreatedAt,
            UpdatedAtUtc = CreatedAt
        };
        await using (var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync()) {
            schema.AddRange(project, foreignProject, resource, legacy, plan, unassigned, shadowed, canonical);
            await schema.SaveChangesAsync();
        }

        var bridge = new ProjectNodeScopeBridge(CreateOwnerFactory(application, reads),
            scope.ServiceProvider.GetRequiredService<ProjectStructureProjectionQueryService>(),
            scope.ServiceProvider.GetRequiredService<IPromptArtifactProjectionQueryService>(),
            scope.ServiceProvider.GetRequiredService<ResourcesService>(),
            scope.ServiceProvider.GetRequiredService<TestLabService>());
        Assert.Equal(new ProjectNodeScopeResolution(true, false, false, ProjectObjectType.Connector, "webhook-endpoint"),
            await bridge.ResolveAsync(project.Id, new($"resource:{resource.Id}")));
        Assert.Equal(new ProjectNodeScopeResolution(false, true, false, ProjectObjectType.Connector, "webhook-endpoint"),
            await bridge.ResolveAsync(foreignProject.Id, new($"resource:{resource.Id}")));
        Assert.Equal(new ProjectNodeScopeResolution(true, false, false, ProjectObjectType.Link, string.Empty),
            await bridge.ResolveAsync(project.Id, new($"resource:{legacy.Id}")));
        Assert.Equal(new ProjectNodeScopeResolution(true, false, false, ProjectObjectType.TestPlan, string.Empty),
            await bridge.ResolveAsync(project.Id, new($"test-plan:{plan.Id}")));
        Assert.Equal(new ProjectNodeScopeResolution(false, true, false, ProjectObjectType.TestPlan, string.Empty),
            await bridge.ResolveAsync(foreignProject.Id, new($"test-plan:{plan.Id}")));
        var absent = new ProjectNodeScopeResolution(false, false, false, null, string.Empty);
        Assert.Equal(absent, await bridge.ResolveAsync(project.Id, new($"resource:{Guid.NewGuid()}")));
        Assert.Equal(absent, await bridge.ResolveAsync(project.Id, new($"test-plan:{Guid.NewGuid()}")));
        Assert.Equal(absent, await bridge.ResolveAsync(project.Id, new($"test-plan:{unassigned.Id}")));
        Assert.Equal(new ProjectNodeScopeResolution(true, false, true, ProjectObjectType.ProjectBlock, "architecture"),
            await bridge.ResolveAsync(project.Id, new(canonical.NodeKey)));
    }

    [Fact]
    public async Task Resource_catalog_resolves_more_projects_than_the_source_reference_cap_in_one_bulk_query() {
        var projectReads = new ProjectQueryProbe();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.AddScoped<IProjectRecordQueryService>(provider =>
                new ObservedProjectQueries(provider.GetRequiredService<ProjectRecordQueryService>(), projectReads))
        });
        var projects = Enumerable.Range(0, ProjectRecordQueryLimits.MaximumReferenceCount + 1)
            .Select(index => new Project { Name = $"Catalog project {index:D4}" }).ToArray();
        var projectNames = projects.ToDictionary(project => project.Id, project => project.Name);
        var missingProjectId = Guid.NewGuid();
        await using (var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync()) {
            schema.AddRange(projects);
            schema.AddRange(projects.Select((project, index) => CreateResource(project.Id, $"Catalog resource {index:D4}")));
            schema.AddRange(CreateResource(missingProjectId, "Missing project resource"),
                CreateResource(Guid.Empty, "Unassigned resource"));
            await schema.SaveChangesAsync();
        }
        await using var scope = application.Services.CreateAsyncScope();
        var resources = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        projectReads.Reset();
        var catalog = await resources.ListAsync();

        Assert.Equal(projects.Length + 2, catalog.Count);
        Assert.Equal(1, projectReads.BulkCalls);
        Assert.Equal(projects.Length + 1, projectReads.ProjectIds.Count);
        Assert.DoesNotContain(Guid.Empty, projectReads.ProjectIds);
        Assert.Equal(projects.Length, catalog.Count(item => projectNames.ContainsKey(item.ProjectId)));
        Assert.All(catalog.Where(item => projectNames.ContainsKey(item.ProjectId)), item =>
            Assert.Equal(projectNames[item.ProjectId], item.ProjectName));
        Assert.Equal("Unknown project", Assert.Single(catalog, item => item.ProjectId == missingProjectId).ProjectName);
        Assert.Equal("Unknown project", Assert.Single(catalog, item => item.ProjectId == Guid.Empty).ProjectName);
    }

    [Fact]
    public async Task Assembly_mutation_reads_preserve_the_outer_serializable_snapshot_and_transaction_ownership() {
        var reads = new OwnerReadProbe();
        await using var application = await CreateObservedApplicationAsync(reads);
        await using var scope = application.Services.CreateAsyncScope();
        var resources = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var testPlans = scope.ServiceProvider.GetRequiredService<TestLabService>();
        var coordination = scope.ServiceProvider.GetRequiredService<CoordinatedDatabaseTransaction>();
        var assembly = new ProjectStructureAssemblyService(
            CreateOwnerFactory(application, reads),
            [new ProjectResourceProjectionContributor(resources), new TestPlanProjectionContributor(testPlans)],
            new SystemClock(), coordination);
        var project = new Project { Name = "Serializable projection owner" };
        var resource = CreateResource(project.Id, "Original resource");
        var plan = CreatePlan(project.Id, "Original plan", CreatedAt);
        await using (var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync()) {
            schema.AddRange(project, resource, plan);
            await schema.SaveChangesAsync();
        }

        await using var owner = await CreateOwnerFactory(application, reads).CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        reads.Reset();
        var initial = await assembly.LoadAsync(owner, project.Id);
        AssertEnlistedReads(reads, owner, transaction);
        Assert.Equal("Original resource", Assert.Single(initial.Nodes, node => node.NodeKey == $"resource:{resource.Id}").Title);
        Assert.Equal("Original plan", Assert.Single(initial.Nodes, node => node.NodeKey == $"test-plan:{plan.Id}").Title);
        await Assert.ThrowsAsync<InvalidOperationException>(() => resources.ListProjectProjectionFactsForMutationAsync(project.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => testPlans.ListProjectProjectionFactsForMutationAsync(project.Id));

        var laterResource = CreateResource(project.Id, "Inserted after snapshot");
        var laterPlan = CreatePlan(project.Id, "Plan inserted after snapshot", CreatedAt.AddHours(1));
        await using (var independent = await application.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>()
            .CreateDbContextAsync()) {
            (await independent.Set<ProjectResource>().SingleAsync(item => item.Id == resource.Id)).Name = "Committed resource edit";
            independent.Add(laterResource);
            await independent.SaveChangesAsync();
        }
        await using (var independent = await application.Services.GetRequiredService<IDbContextFactory<TestLabDbContext>>()
            .CreateDbContextAsync()) {
            (await independent.Set<TestPlan>().SingleAsync(item => item.Id == plan.Id)).Title = "Committed plan edit";
            independent.Add(laterPlan);
            await independent.SaveChangesAsync();
        }

        using (coordination.Enter(owner)) {
            reads.Reset();
            var currentResources = await resources.ListProjectProjectionFactsAsync(project.Id);
            var currentPlans = await testPlans.ListProjectProjectionFactsAsync(project.Id);
            Assert.Equal(2, currentResources.Count);
            Assert.Equal(2, currentPlans.Count);
            Assert.Equal("Committed resource edit", Assert.Single(currentResources, item => item.Id == resource.Id).Name);
            Assert.Equal("Committed plan edit", Assert.Single(currentPlans, item => item.Id == plan.Id).Title);
            Assert.Equal(2, reads.Queries.Count);
            Assert.All(reads.Queries, query => {
                Assert.Null(query.Transaction);
                Assert.NotSame(owner.Database.GetDbConnection(), query.Connection);
            });

            reads.Reset();
            var repeated = await assembly.LoadAsync(owner, project.Id);
            AssertEnlistedReads(reads, owner, transaction);
            Assert.Equal(2, repeated.Nodes.Count);
            Assert.Equal("Original resource", Assert.Single(repeated.Nodes, node => node.NodeKey == $"resource:{resource.Id}").Title);
            Assert.Equal("Original plan", Assert.Single(repeated.Nodes, node => node.NodeKey == $"test-plan:{plan.Id}").Title);

            reads.Reset();
            var moved = await assembly.UpdatePositionsAsync(owner, project.Id, [
                new($"resource:{resource.Id}", 120, 130),
                new($"test-plan:{plan.Id}", 220, 230),
                new($"resource:{laterResource.Id}", 320, 330),
                new($"test-plan:{laterPlan.Id}", 420, 430)
            ]);
            Assert.Equal([$"resource:{resource.Id}", $"test-plan:{plan.Id}"], moved);
            AssertEnlistedReads(reads, owner, transaction);
            Assert.Same(transaction, owner.Database.CurrentTransaction);
            Assert.Same(owner.Database.GetDbConnection(), transaction.GetDbTransaction().Connection);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => resources.ListProjectProjectionFactsForMutationAsync(project.Id));
        await transaction.CommitAsync();
        await using var readback = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        var layouts = await readback.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(item => item.ProjectId == project.Id).ToDictionaryAsync(item => item.NodeKey);
        Assert.Equal(2, layouts.Count);
        Assert.Equal((120d, 130d), (layouts[$"resource:{resource.Id}"].PositionX, layouts[$"resource:{resource.Id}"].PositionY));
        Assert.Equal((220d, 230d), (layouts[$"test-plan:{plan.Id}"].PositionX, layouts[$"test-plan:{plan.Id}"].PositionY));
    }

    private static void AssertEnlistedReads(OwnerReadProbe reads, WorkbenchDbContext owner, IDbContextTransaction transaction) {
        Assert.Equal(1, reads.ResourceQueries);
        Assert.Equal(1, reads.TestPlanQueries);
        Assert.All(reads.Queries, query => {
            Assert.Same(owner.Database.GetDbConnection(), query.Connection);
            Assert.Same(transaction.GetDbTransaction(), query.Transaction);
            Assert.Equal(IsolationLevel.Serializable, query.IsolationLevel);
        });
    }

    private static ProjectResource CreateResource(Guid projectId, string name, string connectorKey = "") => new() {
        ProjectId = projectId,
        Name = name,
        ResourceKind = connectorKey.Length == 0 ? ResourceKind.WebLink : null,
        ConnectorPluginKey = connectorKey,
        ConfigSchemaVersion = connectorKey.Length == 0 ? string.Empty : "1",
        LocationOrIdentifier = "https://example.invalid/projection",
        Description = "Owner projection description",
        ConfigJson = "{\"ownerOnly\":true}",
        ValidationStatus = ResourceValidationStatus.Warning,
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = CreatedAt.AddMinutes(1)
    };

    private static TestPlan CreatePlan(Guid? projectId, string title, DateTimeOffset updatedAt) => new() {
        ProjectId = projectId,
        Title = title,
        Phase = "Verification",
        CoverageGoal = "Preserve owner projection behavior",
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = updatedAt
    };

    private static void AssertBinding(ProjectObjectRecord node, string route, string kind, Guid artifactId) {
        Assert.Equal(route, node.Binding.Route);
        Assert.Equal(kind, node.Binding.ExternalArtifactKind);
        Assert.Equal(artifactId, node.Binding.ExternalArtifactId);
    }

    private static Task<TestApplication> CreateObservedApplicationAsync(OwnerReadProbe reads) =>
        TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.ConfigureDbContext<ResourcesDbContext>(options => options.AddInterceptors(reads), ServiceLifetime.Singleton);
                services.ConfigureDbContext<TestLabDbContext>(options => options.AddInterceptors(reads), ServiceLifetime.Singleton);
            }
        });

    private static IDbContextFactory<WorkbenchDbContext> CreateOwnerFactory(TestApplication application, OwnerReadProbe reads) =>
        new PooledDbContextFactory<WorkbenchDbContext>(new DbContextOptionsBuilder<WorkbenchDbContext>(
            application.Services.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(reads).Options);

    private sealed class OwnerReadProbe : DbCommandInterceptor {
        public int ResourceQueries { get; private set; }
        public int TestPlanQueries { get; private set; }
        public List<OwnerReadObservation> Queries { get; } = [];

        public void Reset() {
            ResourceQueries = 0;
            TestPlanQueries = 0;
            Queries.Clear();
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {
            Observe(command, eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) {
            Observe(command, eventData);
            return ValueTask.FromResult(result);
        }

        private void Observe(DbCommand command, CommandEventData eventData) {
            if (command.CommandText.Contains("\"Resources_ProjectResources\"", StringComparison.Ordinal)) {
                Assert.IsType<ResourcesDbContext>(eventData.Context);
                ResourceQueries++;
                Queries.Add(new(command.Connection, command.Transaction, command.Transaction?.IsolationLevel));
            }
            if (command.CommandText.Contains("\"TestLab_TestPlans\"", StringComparison.Ordinal)) {
                Assert.IsType<TestLabDbContext>(eventData.Context);
                TestPlanQueries++;
                Queries.Add(new(command.Connection, command.Transaction, command.Transaction?.IsolationLevel));
            }
        }
    }

    private sealed record OwnerReadObservation(DbConnection? Connection, DbTransaction? Transaction, IsolationLevel? IsolationLevel);

    private sealed class ProjectQueryProbe {
        public int BulkCalls { get; set; }
        public IReadOnlyCollection<Guid> ProjectIds { get; set; } = [];

        public void Reset() {
            BulkCalls = 0;
            ProjectIds = [];
        }
    }

    private sealed class ObservedProjectQueries(ProjectRecordQueryService inner, ProjectQueryProbe probe) : IProjectRecordQueryService {
        public Task<ProjectRecordQueryItem?> GetAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            inner.GetAsync(projectId, cancellationToken);

        public Task<IReadOnlyList<ProjectRecordQueryItem>> GetManyAsync(
            IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default) {
            probe.BulkCalls++;
            probe.ProjectIds = projectIds.ToArray();
            return inner.GetManyAsync(projectIds, cancellationToken);
        }

        public Task<ProjectRecordPage> SearchAsync(ProjectRecordQuery query, CancellationToken cancellationToken = default) =>
            inner.SearchAsync(query, cancellationToken);
    }
}
