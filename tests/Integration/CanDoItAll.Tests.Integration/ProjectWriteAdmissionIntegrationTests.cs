using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectWriteAdmissionIntegrationTests {
    [Fact]
    public async Task Lifetime_survives_restart_and_copied_project_fields_do_not_admit_another_database_profile() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-write-admission-restart");
        var originalProfile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = originalProfile };
        var projectId = Guid.NewGuid();
        ProjectWriteAdmission admission;
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var scope = original.Services.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Original lifetime" })).IsSuccess);
            admission = Assert.IsType<ProjectWriteAdmission>(await scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        Assert.Equal(admission, await restartedScope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScope = other.Services.CreateAsyncScope();
        var otherServices = otherScope.ServiceProvider;
        await using var target = await otherServices.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var copied = new Project { Id = projectId, Name = "Transferred identity", Slug = "transferred-identity" };
        target.Add(copied);
        target.Entry(copied).Property(project => project.LifetimeId).CurrentValue = admission.LifetimeId;
        await target.SaveChangesAsync();
        var otherAdmissions = otherServices.GetRequiredService<ProjectWriteAdmissionService>();
        var targetAdmission = Assert.IsType<ProjectWriteAdmission>(await otherAdmissions.CaptureAsync(projectId));
        Assert.Equal(admission.ProjectId, targetAdmission.ProjectId);
        Assert.Equal(admission.LifetimeId, targetAdmission.LifetimeId);
        Assert.NotEqual(admission.DatabaseProfileId, targetAdmission.DatabaseProfileId);
        await using var mutation = await SerializableMutationScope.BeginAsync(target, ProjectMutationScopeKeys.ForProject(projectId), CancellationToken.None);
        using (otherServices.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(target)) {
            await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => otherAdmissions.RequireForMutationAsync(admission));
            await otherAdmissions.RequireForMutationAsync(targetAdmission);
        }
        await mutation.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Deletion_retains_the_old_lifetime_and_a_loaded_editor_cannot_overwrite_same_id_recreation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var projectId = Guid.NewGuid();
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "Original" })).IsSuccess);
        var editor = await projects.GetAsync(projectId);
        var oldAdmission = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(projectId));
        Assert.Equal(oldAdmission.LifetimeId, editor.ExpectedLifetimeId);
        await projects.DeleteAsync(projectId);
        Assert.Null(await admissions.CaptureAsync(projectId));
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "Restored with same public id" })).IsSuccess);
        var current = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(projectId));
        Assert.NotEqual(oldAdmission.LifetimeId, current.LifetimeId);
        editor.Name = "Late old editor";
        var rejected = await projects.SaveAsync(editor);
        Assert.True(rejected.IsFailure);
        Assert.Equal(ProjectErrorCodes.LifetimeChanged, Assert.Single(rejected.Errors).Code);
        Assert.Equal("Restored with same public id", (await projects.GetAsync(projectId)).Name);
        await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var retirement = await context.Set<ProjectRetirementRecord>().SingleAsync(record => record.ProjectId == projectId);
        Assert.Equal(oldAdmission.LifetimeId, retirement.LifetimeId);
        Assert.Empty(context.Model.FindEntityType(typeof(ProjectRetirementRecord))!.GetForeignKeys());
        await using var mutation = await SerializableMutationScope.BeginAsync(context, ProjectMutationScopeKeys.ForProject(projectId), CancellationToken.None);
        using (services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context)) {
            await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => admissions.RequireForMutationAsync(oldAdmission));
            await admissions.RequireForMutationAsync(current);
        }
        await mutation.CommitAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_postcommit_search_cannot_resurrect_a_deleted_project_or_overwrite_its_new_lifetime(bool recreate) {
        await using var application = await TestApplication.CreateAsync();
        await using var delayedScope = application.Services.CreateAsyncScope();
        await using var deletionScope = application.Services.CreateAsyncScope();
        var factory = new PausingProjectsFactory(delayedScope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>());
        var delayed = ActivatorUtilities.CreateInstance<ProjectsService>(delayedScope.ServiceProvider, factory);
        var projectId = Guid.NewGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var save = delayed.CreateAsync(projectId, new() { Name = "Old delayed search" }, timeout.Token);
        Result<Guid> result;
        try {
            await factory.Paused.Task.WaitAsync(timeout.Token);
            var currentProjects = deletionScope.ServiceProvider.GetRequiredService<ProjectsService>();
            await currentProjects.DeleteAsync(projectId, timeout.Token);
            if (recreate) {
                Assert.True((await currentProjects.CreateAsync(projectId, new() { Name = "Current restored search" }, timeout.Token)).IsSuccess);
            }
        } finally {
            factory.Resume.TrySetResult(true);
            result = await save;
        }
        Assert.True(result.IsSuccess);
        await using var search = await application.Services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        var rows = await search.Set<SearchDocument>().Where(document => document.ProjectId == projectId).ToArrayAsync();
        if (recreate) {
            Assert.Equal("Current restored search", Assert.Single(rows).Title);
        } else {
            Assert.Empty(rows);
        }
    }

    [Fact]
    public async Task Admission_read_and_search_stage_use_the_exact_owner_transaction_and_rollback_together() {
        await using var application = await TestApplication.CreateAsync();
        var canonical = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(canonical.Profile);
        var probe = new CommandProbe();
        var projectOptions = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(projectOptions, canonical.Profile);
        projectOptions.AddInterceptors(probe);
        var searchOptions = new DbContextOptionsBuilder<SearchDbContext>();
        AppDbContextOptionsConfigurator.Configure(searchOptions, canonical.Profile);
        searchOptions.AddInterceptors(probe);
        var admissions = new ProjectWriteAdmissionService(new Factory<ProjectsDbContext>(options => new(options), projectOptions.Options),
            projectOptions.Options, coordinator, canonical);
        var search = new SearchIndexService(new Factory<SearchDbContext>(options => new(options), searchOptions.Options),
            application.Services.GetRequiredService<IClock>(), searchOptions.Options, coordinator);
        var project = new Project { Id = Guid.NewGuid(), Name = "Uncommitted admitted project", Slug = "uncommitted-admitted-project" };
        var admission = new ProjectWriteAdmission(canonical.Profile.Profile.Id, project.Id, project.LifetimeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireForMutationAsync(admission));
        await using (var owner = await application.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            await using var mutation = await SerializableMutationScope.BeginAsync(owner, ProjectMutationScopeKeys.ForProject(project.Id), CancellationToken.None);
            owner.Add(project);
            await owner.SaveChangesAsync();
            var transaction = owner.Database.CurrentTransaction!.GetDbTransaction();
            using (coordinator.Enter(owner)) {
                probe.Commands.Clear();
                await admissions.RequireForMutationAsync(admission);
                await search.UpsertForMutationAsync(new(SearchDocument.ProjectSourceType, project.Id.ToString(), "Projects",
                    project.Name, "Summary", "Body", $"/projects?projectId={project.Id}", project.Id));
                Assert.True(probe.Commands.Count >= 3);
                Assert.All(probe.Commands, command => {
                    Assert.Same(owner.Database.GetDbConnection(), command.Connection);
                    Assert.Same(transaction, command.Transaction);
                    Assert.Equal(IsolationLevel.Serializable, command.Transaction.IsolationLevel);
                });
                Assert.Null(await admissions.CaptureAsync(project.Id));
                Assert.Empty(await search.SearchAsync(project.Name));
            }
        }
        Assert.Null(await admissions.CaptureAsync(project.Id));
        Assert.Empty(await search.SearchAsync(project.Name));
        await Assert.ThrowsAsync<InvalidOperationException>(() => search.UpsertForMutationAsync(
            new(SearchDocument.ProjectSourceType, project.Id.ToString(), "Projects", project.Name, "", "", "/projects", project.Id)));
    }

    private sealed class PausingProjectsFactory(IDbContextFactory<ProjectsDbContext> inner) : IDbContextFactory<ProjectsDbContext> {
        private int creationCount;
        public TaskCompletionSource<bool> Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProjectsDbContext CreateDbContext() => throw new NotSupportedException("This asynchronous creation path must not block synchronously.");

        public async Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (Interlocked.Increment(ref creationCount) == 2) {
                Paused.TrySetResult(true);
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return await inner.CreateDbContextAsync(cancellationToken);
        }
    }

    private sealed class Factory<TContext>(Func<DbContextOptions<TContext>, TContext> create, DbContextOptions<TContext> options)
        : IDbContextFactory<TContext> where TContext : DbContext {
        public TContext CreateDbContext() => create(options);
    }

    private sealed record CommandObservation(DbConnection Connection, DbTransaction Transaction);

    private sealed class CommandProbe : DbCommandInterceptor {
        public List<CommandObservation> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.Transaction is { } transaction) {
                Commands.Add(new(command.Connection!, transaction));
            }
            return ValueTask.FromResult(result);
        }
    }
}
