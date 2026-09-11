using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ResourceTestLabAdmissionIntegrationTests {
    [Fact]
    public async Task Retained_reference_bindings_survive_restart_without_becoming_the_recreated_projects_admission() {
        await using var environment = CanDoItAllTestEnvironment.Create("resource-testlab-lifetime-restart");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("owner") };
        var projectId = Guid.NewGuid();
        Guid resourceId;
        Guid planId;
        ProjectWriteAdmission original;
        await using (var application = await TestApplication.CreateAsync(harness)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            original = await CreateProjectAsync(services, projectId);
            var resource = await services.GetRequiredService<ResourcesService>().SaveAsync(ResourceEditor(original));
            var plan = await services.GetRequiredService<TestLabService>().SaveAsync(PlanEditor(original));
            Assert.True(resource.IsSuccess);
            Assert.True(plan.IsSuccess);
            resourceId = resource.Value;
            planId = plan.Value;
            await services.GetRequiredService<ProjectsService>().DeleteAsync(projectId);
            Assert.NotEqual(original.LifetimeId, (await CreateProjectAsync(services, projectId)).LifetimeId);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var current = restartedScope.ServiceProvider;
        var resources = current.GetRequiredService<ResourcesService>();
        var plans = current.GetRequiredService<TestLabService>();
        var resourceEditor = await resources.GetAsync(resourceId);
        var planEditor = await plans.GetAsync(planId);
        Assert.Equal(original, resourceEditor.ExpectedProjectAdmission);
        Assert.Equal(original, planEditor.ExpectedProjectAdmission);
        var resourceSummary = Assert.Single(await resources.ListAsync());
        var planSummary = Assert.Single(await plans.ListAsync());
        Assert.Equal(resourceId, resourceSummary.Id);
        Assert.Equal("Unknown project", resourceSummary.ProjectName);
        Assert.Equal(original.LifetimeId, resourceSummary.ProjectLifetimeId);
        Assert.Equal(planId, planSummary.Id);
        Assert.Equal(original.LifetimeId, planSummary.ProjectLifetimeId);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => resources.SaveAsync(resourceEditor));
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => plans.SaveAsync(planEditor));
        Assert.Empty(await resources.ListProjectProjectionFactsAsync(projectId));
        Assert.Empty(await plans.ListProjectProjectionFactsAsync(projectId));
        Assert.Null(await resources.ReadProjectionScopeAsync(resourceId));
        Assert.Null(await plans.ReadProjectionScopeAsync(planId));
        var snapshots = current.GetRequiredService<IResourceSourceSnapshotProvider>();
        Assert.Empty((await snapshots.ReadSnapshotAsync(new(ResourceId: null, ProjectId: projectId))).Items);
        Assert.NotEmpty((await snapshots.ReadSnapshotAsync(new(ResourceId: resourceId, ProjectId: null))).Items);
        var selected = Assert.Single(await current.GetRequiredService<ProjectWriteSelectionQuery>().ListAsync());
        Assert.NotEqual(original, selected.Admission);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task Delayed_owner_save_and_postcommit_search_keep_the_captured_lifetime_across_restart(bool testPlan, int pauseAt) {
        await using var environment = CanDoItAllTestEnvironment.Create("resource-testlab-delayed-lifetime");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("owner") };
        var projectId = Guid.NewGuid();
        ProjectWriteAdmission original;
        var committedId = Guid.Empty;
        await using (var application = await TestApplication.CreateAsync(harness)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            original = await CreateProjectAsync(services, projectId);
            var resourceFactory = new PausingFactory<ResourcesDbContext>(services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>(), pauseAt);
            var planFactory = new PausingFactory<TestLabDbContext>(services.GetRequiredService<IDbContextFactory<TestLabDbContext>>(), pauseAt);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var pending = testPlan
                ? PlanService(services, planFactory).SaveAsync(PlanEditor(original), cancellation.Token)
                : ResourceService(services, resourceFactory).SaveAsync(ResourceEditor(original), cancellation.Token);
            var paused = testPlan ? planFactory.Paused : resourceFactory.Paused;
            var resume = testPlan ? planFactory.Resume : resourceFactory.Resume;
            await paused.Task.WaitAsync(TimeSpan.FromSeconds(20));
            try {
                await services.GetRequiredService<ProjectsService>().DeleteAsync(projectId);
                Assert.NotEqual(original.LifetimeId, (await CreateProjectAsync(services, projectId)).LifetimeId);
            } finally {
                resume.TrySetResult();
            }
            if (pauseAt == 1) {
                await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => pending);
            } else if (testPlan) {
                var failure = await Assert.ThrowsAsync<TestPlanCommittedSaveException>(() => pending);
                committedId = failure.TestPlanId;
                Assert.IsType<ProjectWriteAdmissionRejectedException>(failure.InnerException);
            } else {
                var failure = await Assert.ThrowsAsync<ResourceCommittedMutationException>(() => pending);
                committedId = failure.ResourceId;
                Assert.Equal(ResourceMutationKind.Save, failure.MutationKind);
                Assert.IsType<ProjectWriteAdmissionRejectedException>(failure.InnerException);
            }
            await using var search = await services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
            Assert.False(await search.Set<SearchDocument>().AnyAsync(document => document.ProjectId == projectId &&
                (document.SourceType == "resource" || document.SourceType == "test-plan")));
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var current = restartedScope.ServiceProvider;
        if (testPlan) {
            await using var owner = await current.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync();
            var saved = await owner.Set<TestPlan>().ToArrayAsync();
            Assert.Equal(pauseAt == 2 ? 1 : 0, saved.Length);
            if (pauseAt == 2) {
                Assert.NotEqual(Guid.Empty, committedId);
                Assert.Equal(committedId, Assert.Single(saved).Id);
                Assert.Equal(original.LifetimeId, Assert.Single(saved).ProjectLifetimeId);
                Assert.Equal(original, (await current.GetRequiredService<TestLabService>().GetAsync(saved[0].Id)).ExpectedProjectAdmission);
            }
        } else {
            await using var owner = await current.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
            var saved = await owner.Set<ProjectResource>().ToArrayAsync();
            Assert.Equal(pauseAt == 2 ? 1 : 0, saved.Length);
            if (pauseAt == 2) {
                Assert.NotEqual(Guid.Empty, committedId);
                Assert.Equal(committedId, Assert.Single(saved).Id);
                Assert.Equal(original.LifetimeId, Assert.Single(saved).ProjectLifetimeId);
                Assert.Equal(original, (await current.GetRequiredService<ResourcesService>().GetAsync(saved[0].Id)).ExpectedProjectAdmission);
            }
        }
    }

    [Fact]
    public async Task Promotion_rejects_a_retired_selection_and_does_not_reuse_its_retained_resource_for_a_new_lifetime() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = Guid.NewGuid();
        var original = await CreateProjectAsync(services, projectId);
        var storageId = Guid.NewGuid();
        var config = new StorageObjectResourceConfig(ResourceFileSourceKey.ForStorage(storageId).Value, storageId,
            StorageProviderKind.FileSystem, StorageLocatorKind.RelativePath, "retained.txt", "retained.txt", "text/plain", 12);
        var writer = services.GetRequiredService<IStorageObjectResourceWriter>();
        var request = new StorageObjectResourceWriteRequest(projectId, "Original resource", ResourceSensitivity.Normal, config, original);
        var first = await writer.SaveAsync(request);
        await services.GetRequiredService<ProjectsService>().DeleteAsync(projectId);
        var replacement = await CreateProjectAsync(services, projectId);

        var denied = await Assert.ThrowsAsync<ResourcePromotionException>(() => writer.SaveAsync(request));

        Assert.Equal(ResourcePromotionFailureCode.TargetUnavailable, denied.Code);
        var current = await writer.SaveAsync(request with { ExpectedProjectAdmission = replacement });
        var repeat = await writer.SaveAsync(request with { ExpectedProjectAdmission = replacement });
        Assert.True(current.Created);
        Assert.NotEqual(first.ResourceId, current.ResourceId);
        Assert.False(repeat.Created);
        Assert.Equal(current.ResourceId, repeat.ResourceId);
        await using var owner = await services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        Assert.Equal(original.LifetimeId, (await owner.Set<ProjectResource>().SingleAsync(row => row.Id == first.ResourceId)).ProjectLifetimeId);
        Assert.Equal(replacement.LifetimeId, (await owner.Set<ProjectResource>().SingleAsync(row => row.Id == current.ResourceId)).ProjectLifetimeId);
    }

    [Fact]
    public async Task Unbound_orphans_stay_readable_without_automatic_admission_and_global_plans_remain_writable() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var missingId = Guid.NewGuid();
        var resource = new ProjectResource { ProjectId = missingId, Name = "Unbound resource", ConnectorPluginKey = WebhookResourceConnectorPlugin.PluginKey,
            ConfigSchemaVersion = "1.0", ConfigJson = "{\"endpointUrl\":\"https://example.invalid/hook\",\"method\":\"post\"}" };
        var plan = new TestPlan { ProjectId = missingId, Title = "Unbound historical plan" };
        await using (var owner = await services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync()) {
            owner.Add(resource);
            await owner.SaveChangesAsync();
        }
        await using (var owner = await services.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync()) {
            owner.Add(plan);
            await owner.SaveChangesAsync();
        }
        await CreateProjectAsync(services, missingId);
        var resources = services.GetRequiredService<ResourcesService>();
        var plans = services.GetRequiredService<TestLabService>();
        var resourceEditor = await resources.GetAsync(resource.Id);
        var planEditor = await plans.GetAsync(plan.Id);
        Assert.Null(resourceEditor.ExpectedProjectAdmission);
        Assert.Null(planEditor.ExpectedProjectAdmission);
        Assert.True((await resources.SaveAsync(resourceEditor)).IsFailure);
        Assert.True((await plans.SaveAsync(planEditor)).IsFailure);
        var global = await plans.SaveAsync(new() { Title = "Independent global plan" });
        Assert.True(global.IsSuccess);
        var loaded = await plans.GetAsync(global.Value);
        Assert.Null(loaded.ProjectId);
        Assert.Null(loaded.ExpectedProjectAdmission);
        loaded.Title = "Edited global plan";
        Assert.True((await plans.SaveAsync(loaded)).IsSuccess);
        await resources.DeleteAsync(resource.Id, expectedProjectAdmission: null);
        Assert.Null((await resources.GetAsync(resource.Id)).Id);
        Assert.Equal(plan.Id, (await plans.GetAsync(plan.Id)).Id);
    }

    [Fact]
    public async Task Project_owner_selection_keeps_profile_identity_and_stale_owner_ids_cannot_be_recreated_by_edit() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var selected = await CreateProjectAsync(services, Guid.NewGuid());
        var foreign = new ProjectWriteAdmission(Guid.NewGuid(), selected.ProjectId, selected.LifetimeId);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => services.GetRequiredService<ResourcesService>().SaveAsync(ResourceEditor(foreign)));
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => services.GetRequiredService<TestLabService>().SaveAsync(PlanEditor(foreign)));
        var resource = ResourceEditor(selected);
        resource.Id = Guid.NewGuid();
        var plan = PlanEditor(selected);
        plan.Id = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ResourcesService>().SaveAsync(resource)).IsFailure);
        Assert.True((await services.GetRequiredService<TestLabService>().SaveAsync(plan)).IsFailure);
        Assert.Equal(selected, Assert.Single(await services.GetRequiredService<ProjectWriteSelectionQuery>().ListAsync()).Admission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_changed_editor_cannot_redirect_an_inflight_save_away_from_its_captured_admission(bool testPlan) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var original = await CreateProjectAsync(services, Guid.NewGuid());
        var later = await CreateProjectAsync(services, Guid.NewGuid());
        var resourceFactory = new PausingFactory<ResourcesDbContext>(services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>(), 1);
        var planFactory = new PausingFactory<TestLabDbContext>(services.GetRequiredService<IDbContextFactory<TestLabDbContext>>(), 1);
        var resource = ResourceEditor(original);
        var plan = PlanEditor(original);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var pending = testPlan
            ? PlanService(services, planFactory).SaveAsync(plan, cancellation.Token)
            : ResourceService(services, resourceFactory).SaveAsync(resource, cancellation.Token);
        var paused = testPlan ? planFactory.Paused : resourceFactory.Paused;
        var resume = testPlan ? planFactory.Resume : resourceFactory.Resume;
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(20));
        resource.ProjectId = later.ProjectId;
        resource.ExpectedProjectAdmission = later;
        plan.ProjectId = later.ProjectId;
        plan.ExpectedProjectAdmission = later;
        resume.TrySetResult();

        var saved = await pending;

        Assert.True(saved.IsSuccess);
        var actual = testPlan
            ? (await services.GetRequiredService<TestLabService>().GetAsync(saved.Value)).ExpectedProjectAdmission
            : (await services.GetRequiredService<ResourcesService>().GetAsync(saved.Value)).ExpectedProjectAdmission;
        Assert.Equal(original, actual);
        await using var search = await services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        var sourceType = testPlan ? "test-plan" : "resource";
        var document = await search.Set<SearchDocument>().SingleAsync(row => row.SourceType == sourceType && row.SourceKey == saved.Value.ToString());
        Assert.Equal(original.ProjectId, document.ProjectId);
    }

    private static async Task<ProjectWriteAdmission> CreateProjectAsync(IServiceProvider services, Guid projectId) {
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Current project" })).IsSuccess);
        return Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
    }

    private static ResourceEditorModel ResourceEditor(ProjectWriteAdmission admission) => new() {
        ProjectId = admission.ProjectId, ExpectedProjectAdmission = admission, Name = "Admitted resource",
        ConnectorPluginKey = WebhookResourceConnectorPlugin.PluginKey, ConfigSchemaVersion = "1.0",
        ConfigJson = "{\"endpointUrl\":\"https://example.invalid/hook\",\"method\":\"post\"}"
    };

    private static TestPlanEditorModel PlanEditor(ProjectWriteAdmission admission) => new() {
        ProjectId = admission.ProjectId, ExpectedProjectAdmission = admission, Title = "Admitted plan"
    };

    private static ResourcesService ResourceService(IServiceProvider services, IDbContextFactory<ResourcesDbContext> factory) => new(
        factory, services.GetRequiredService<IProjectRecordQueryService>(), services.GetRequiredService<IClock>(),
        services.GetRequiredService<IActivityStream>(), services.GetRequiredService<ISearchIndexService>(),
        services.GetRequiredService<ResourceConnectorPluginRegistry>(), services.GetRequiredService<DbContextOptions<ResourcesDbContext>>(),
        services.GetRequiredService<CoordinatedDatabaseTransaction>(), services.GetRequiredService<ProjectWriteAdmissionService>());

    private static TestLabService PlanService(IServiceProvider services, IDbContextFactory<TestLabDbContext> factory) => new(
        factory, services.GetRequiredService<IClock>(), services.GetRequiredService<IActivityStream>(),
        services.GetRequiredService<ISearchIndexService>(), services.GetRequiredService<DbContextOptions<TestLabDbContext>>(),
        services.GetRequiredService<CoordinatedDatabaseTransaction>(), services.GetRequiredService<ProjectWriteAdmissionService>());

    private sealed class PausingFactory<TContext>(IDbContextFactory<TContext> inner, int pauseAt) : IDbContextFactory<TContext> where TContext : DbContext {
        private int creationCount;
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TContext CreateDbContext() => throw new NotSupportedException("This fixture only uses asynchronous owner context creation.");
        public async Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (Interlocked.Increment(ref creationCount) == pauseAt) {
                Paused.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return await inner.CreateDbContextAsync(cancellationToken);
        }
    }
}
