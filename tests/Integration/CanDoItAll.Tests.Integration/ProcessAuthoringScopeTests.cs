using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringScopeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task First_project_override_rechecks_inherited_revision_under_the_database_lock(bool existingGlobal) {
        await using var app = await TestApplication.CreateAsync(new());
        await using var reader = app.Services.CreateAsyncScope();
        var command = await CommandAsync(reader.ServiceProvider);
        var key = new ProcessDefinitionCatalogItemKey(command.Address.DefinitionKey);
        if (existingGlobal) {
            await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key.Value, ProcessDefinitionEditorCommandKind.SaveDraft, "Earlier global draft");
        }
        await using var projectScope = app.Services.CreateAsyncScope();
        var workspace = projectScope.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>();
        var scope = ProcessWorkspaceShellScope.ForProject(command.Address.ProjectId);
        var baseline = await workspace.ReadAsync(scope, key, default);
        await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key.Value, ProcessDefinitionEditorCommandKind.SaveDraft, "Newer global draft");
        var submitted = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        submitted.Definition.DisplayName = "Stale project override";
        var operation = Guid.NewGuid();
        var fingerprint = ProcessAuthoringCodec.Hash("first override race");
        var result = await workspace.CommitAsync(baseline, operation, fingerprint, submitted, ProcessAuthoringLifecycle.Draft, false, null, default);
        Assert.Equal(ProcessAuthoringOutcome.Conflict, result.Outcome);
        Assert.Equal(0, result.Snapshot!.Revision);
        Assert.Equal(existingGlobal ? 2 : 1, result.Snapshot.InheritedRevision);
        Assert.Equal("Newer global draft", result.Snapshot.Content.Definition.DisplayName);
        Assert.Null(await projectScope.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().ReadAsync(command.Address));
        var replay = await workspace.RecoverAsync(scope, key, baseline.Token, operation, fingerprint, default);
        Assert.Equal(ProcessAuthoringCodec.WriteReceipt(result), ProcessAuthoringCodec.WriteReceipt(replay!));
        var current = await workspace.ReadAsync(scope, key, default);
        Assert.Equal("Newer global draft", current.Content.Definition.DisplayName);
        Assert.NotEqual(baseline.Observation, current.Observation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Captured_write_cannot_cross_project_retirement_or_recreation(bool recreate) {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var command = await CommandAsync(scope.ServiceProvider);
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(command.Address.ProjectId);
        if (recreate) {
            Assert.True((await projects.CreateAsync(command.Address.ProjectId, new() { Name = "Replacement lifetime" })).IsSuccess);
        }
        await using var writer = app.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => writer.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().CommitAsync(command));
        await using var database = await writer.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Empty(await database.AuthoringHeads.ToArrayAsync());
        Assert.Empty(await database.AuthoringReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task Same_project_id_in_another_profile_does_not_admit_the_original_command() {
        await using var first = await TestApplication.CreateAsync(new());
        await using var second = await TestApplication.CreateAsync(new());
        await using var a = first.Services.CreateAsyncScope();
        await using var b = second.Services.CreateAsyncScope();
        var command = await CommandAsync(a.ServiceProvider);
        Assert.True((await b.ServiceProvider.GetRequiredService<ProjectsService>().CreateAsync(command.Address.ProjectId, new() { Name = "Other profile" })).IsSuccess);
        await Assert.ThrowsAsync<InvalidOperationException>(() => b.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().CommitAsync(command));
        await a.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().CommitAsync(command);
        await using var database = await b.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Empty(await database.AuthoringHeads.ToArrayAsync());
        Assert.Empty(await database.AuthoringReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task Concurrent_project_first_writes_use_database_uniqueness_and_one_revision() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var a = app.Services.CreateAsyncScope();
        await using var b = app.Services.CreateAsyncScope();
        var command = await CommandAsync(a.ServiceProvider);
        var results = await Task.WhenAll(a.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().CommitAsync(command),
            b.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().CommitAsync(command with { OperationId = Guid.NewGuid() }));
        Assert.Single(results, item => item.Outcome == ProcessAuthoringOutcome.Accepted);
        Assert.Single(results, item => item.Outcome == ProcessAuthoringOutcome.Conflict);
        Assert.All(results, item => Assert.Equal(1, item.Snapshot!.Revision));
    }

    [Fact]
    public async Task Real_project_mutation_gate_blocks_deletion_until_authoring_commit_finishes() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        await using var deletingScope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var command = await CommandAsync(services);
        var pause = new CommitPause();
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(pause);
        var store = new EfProcessAuthoringStore(new Factory(options.Options), services.GetRequiredService<CoordinatedDatabaseTransaction>(),
            services.GetRequiredService<IProcessAuthoringAdmissionPolicy>(), TimeProvider.System, options.Options);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var commit = store.CommitAsync(command, deadline.Token);
        Task<ProjectDeletionResult>? deletion = null;
        try {
            await pause.Entered.Task.WaitAsync(deadline.Token);
            deletion = deletingScope.ServiceProvider.GetRequiredService<ProjectsService>().DeleteAsync(command.Address.ProjectId, deadline.Token);
            await using var observer = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync(deadline.Token);
            while (await observer.Database.SqlQueryRaw<int>("""
                SELECT count(*)::integer AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock' AND wait_event = 'advisory'
                """).SingleAsync(deadline.Token) == 0) {
                await Task.Delay(20, deadline.Token);
            }
            Assert.False(deletion.IsCompleted);
            Assert.Empty(await observer.AuthoringHeads.ToArrayAsync(deadline.Token));
        } finally {
            pause.Release.TrySetResult();
        }
        Assert.Equal(ProcessAuthoringOutcome.Accepted, (await commit).Outcome);
        await deletion!;
        await using var database = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Equal(command.Address.ProjectLifetimeId, (await database.AuthoringHeads.SingleAsync()).ProjectLifetimeId);
        Assert.Single(await database.AuthoringReceipts.ToArrayAsync());
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => services.GetRequiredService<IProcessAuthoringStore>().CommitAsync(command));
    }

    private static async Task<ProcessAuthoringCommit> CommandAsync(IServiceProvider services) {
        var id = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(id, new() { Name = "Owned authoring project" })).IsSuccess);
        var key = services.GetRequiredService<ProcessTemplatePackLoader>().Load().Definitions[0].Key;
        var workspace = services.GetRequiredService<ProcessAuthoringWorkspace>();
        var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.ForProject(id), new(key), default);
        return new(baseline.Address, ProcessAuthoringAdmissionPolicy.LocalCallerId, Guid.NewGuid(), ProcessAuthoringCodec.Hash("captured project draft"),
            0, baseline.Content, ProcessAuthoringLifecycle.Draft, false);
    }

    private sealed class Factory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class CommitPause : DbTransactionInterceptor {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
