using System.Data.Common;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectChangePublicationTests {
    [Fact]
    public async Task Revoking_the_held_UI_write_grant_after_staging_rolls_back_without_a_hint() {
        var revocation = new RevokeAfterRootWrite();
        await using var app = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(revocation), ServiceLifetime.Singleton)
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var catalog = Subscribe(services);
        var invocation = new ProjectDefinitionInvocation(ProjectMutationCallerScope.LocalWorkspace, new ReceiptAccess());
        var operation = await OperationAsync(services, invocation);
        using var admission = services.GetRequiredService<ProjectChanges>().EnterUiWrite(token => {
            token.ThrowIfCancellationRequested();
            return revocation.Reached ? ValueTask.FromException(new UnauthorizedAccessException("Owned current UI grant revocation.")) : ValueTask.CompletedTask;
        });
        revocation.Armed = true;

        var outcome = await services.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(new(operation,
            ProjectDefinitionRequestVersion.VersionOne, Creation()), invocation);

        Assert.True(revocation.Reached);
        Assert.IsNotType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(outcome);
        Assert.Empty(await ReadAsync(catalog));
        Assert.Empty(await services.GetRequiredService<ProjectsService>().ListAsync());
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Empty(await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Staged_SQL_is_quiet_until_physical_commit_and_rollback_remains_quiet(bool rollback) {
        var barrier = new CommitBarrier { Rollback = rollback };
        var contribution = new RecordedContribution();
        await using var app = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(barrier), ServiceLifetime.Singleton);
                services.AddSingleton<IProjectCommittedChangeSink>(contribution);
            }
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var catalog = Subscribe(services);
        var invocation = new ProjectDefinitionInvocation(ProjectMutationCallerScope.LocalWorkspace, new ReceiptAccess());
        var operation = await OperationAsync(services, invocation);
        barrier.Armed = true;
        var write = services.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(new(operation,
            ProjectDefinitionRequestVersion.VersionOne, Creation()), invocation);
        ProjectDefinitionOutcome<ProjectEditorAcknowledgement> outcome;
        try {
            await barrier.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Empty(await ReadAsync(catalog));
            Assert.Empty(contribution.Projects);
            Assert.Empty(await services.GetRequiredService<ProjectsService>().ListAsync());
        } finally {
            barrier.Release.TrySetResult();
            outcome = await write;
        }
        if (rollback) {
            Assert.IsNotType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(outcome);
            Assert.Empty(await ReadAsync(catalog));
            Assert.Empty(contribution.Projects);
            Assert.Empty(await services.GetRequiredService<ProjectsService>().ListAsync());
        } else {
            var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(outcome);
            Assert.Equal(accepted.Result.Acknowledgement.Project.ProjectId, Assert.Single(await ReadAsync(catalog)).Resource.ResourceId);
            Assert.Equal(accepted.Result.Acknowledgement.Project.ProjectId, Assert.Single(contribution.Projects));
        }
    }

    [Fact]
    public async Task Failed_optional_postcommit_activity_cannot_suppress_the_committed_definition_hint() {
        await using var app = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton<IActivityStream>(new FailingActivity()))
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var catalog = Subscribe(services);
        var invocation = new ProjectDefinitionInvocation(ProjectMutationCallerScope.LocalWorkspace, new ReceiptAccess());
        var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(
            await services.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(new(await OperationAsync(services, invocation),
                ProjectDefinitionRequestVersion.VersionOne, Creation()), invocation));

        Assert.False(accepted.Replayed);
        Assert.Equal(accepted.Result.Acknowledgement.Project.ProjectId, Assert.Single(await ReadAsync(catalog)).Resource.ResourceId);
        Assert.Single(await services.GetRequiredService<ProjectsService>().ListAsync());
    }

    [Fact]
    public async Task Native_definition_commits_publish_targeted_hints_but_replay_and_stale_writes_do_not() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var catalog = Subscribe(services);
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        var invocation = new ProjectDefinitionInvocation(ProjectMutationCallerScope.LocalWorkspace, new ReceiptAccess());
        var create = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(await OperationAsync(services, invocation),
            ProjectDefinitionRequestVersion.VersionOne,
            Creation());
        var editor = Guid.NewGuid();
        using var origin = services.GetRequiredService<EditingOperationContext>().Enter(new(editor, create.Operation.Id));

        var created = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.CreateAsync(create, invocation));
        var projectId = created.Result.Acknowledgement.Project.ProjectId;
        var hint = Assert.Single(await ReadAsync(catalog));
        Assert.Equal(projectId, hint.Resource.ResourceId);
        Assert.Equal(EditingOwner.Project, hint.Resource.Owner);
        Assert.Equal(EditingChangeKind.Created, hint.Kind);
        Assert.Equal(editor, hint.OriginEditorId);
        Assert.Equal(create.Operation.Id, hint.OperationId);
        using var record = Subscribe(services, projectId);
        using var unrelated = Subscribe(services, Guid.NewGuid());
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(projectId)).Value!;
        var update = new ProjectDefinitionRequest<UpdateProjectDefinitionScalarsCommand>(await OperationAsync(services, invocation),
            ProjectDefinitionRequestVersion.VersionOne, new(original.Expectation, original.Scalars with { Name = "Accepted peer name" }));

        Assert.IsType<ProjectDefinitionOutcome<ProjectDefinitionScalarsAcknowledgement>.Accepted>(await writer.UpdateScalarsAsync(update, invocation));
        hint = Assert.Single(await ReadAsync(catalog));
        Assert.Equal(EditingChangeKind.EditableContentChanged, hint.Kind);
        Assert.Equal(update.Operation.Id, hint.OperationId);
        Assert.Equal(hint, Assert.Single(await ReadAsync(record)));
        Assert.Empty(await ReadAsync(unrelated));
        Assert.True(Assert.IsType<ProjectDefinitionOutcome<ProjectDefinitionScalarsAcknowledgement>.Accepted>(
            await writer.UpdateScalarsAsync(update, invocation)).Replayed);
        Assert.IsType<ProjectDefinitionOutcome<ProjectDefinitionScalarsAcknowledgement>.Rejected>(await writer.UpdateScalarsAsync(
            new(await OperationAsync(services, invocation), update.Version, update.Command), invocation));
        Assert.Empty(await ReadAsync(catalog));
        Assert.Empty(await ReadAsync(record));
    }

    [Fact]
    public async Task Native_link_and_deletion_hints_name_actual_affected_projects_once_and_do_not_replay() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var parent = (await projects.SaveAsync(new() { Name = "Observed parent" })).Value;
        var child = (await projects.SaveAsync(new() { Name = "Observed child" })).Value;
        using var catalog = Subscribe(services);
        var link = await ProjectLifecycleTestCommands.CaptureLinkAsync(services, parent, child);

        ProjectLifecycleTestCommands.RequireAccepted(await ProjectLifecycleTestCommands.SubmitAsync(services, link));
        var linked = await ReadAsync(catalog);
        Assert.Equal(new[] { parent, child }.Order(), linked.Select(change => change.Resource.ResourceId!.Value).Order());
        Assert.All(linked, change => Assert.Equal(EditingChangeKind.MetadataChanged, change.Kind));
        Assert.All(linked, change => Assert.Equal(link.Operation.Id, change.OperationId));
        Assert.True(ProjectLifecycleTestCommands.RequireAccepted(await ProjectLifecycleTestCommands.SubmitAsync(services, link)).Replayed);
        Assert.Empty(await ReadAsync(catalog));
        var delete = await ProjectLifecycleTestCommands.CaptureDeleteAsync(services, child);

        ProjectLifecycleTestCommands.RequireAccepted(await ProjectLifecycleTestCommands.SubmitAsync(services, delete));
        var deleted = await ReadAsync(catalog);
        var physical = Assert.Single(deleted, change => change.Resource.ResourceId == child && change.Kind == EditingChangeKind.Deleted);
        var completion = Assert.Single(deleted, change => change.Resource.ResourceId == child && change.Kind == EditingChangeKind.MetadataChanged);
        Assert.True(completion.Cursor.Sequence > physical.Cursor.Sequence);
        Assert.DoesNotContain(deleted, change => change.Resource.ResourceId != child && change.Kind == EditingChangeKind.Deleted);
        Assert.All(deleted, change => Assert.Equal(delete.Operation.Id, change.OperationId));
        Assert.True(ProjectLifecycleTestCommands.RequireAccepted(await ProjectLifecycleTestCommands.SubmitAsync(services, delete)).Replayed);
        Assert.Empty(await ReadAsync(catalog));
    }

    private static IEditingChangeSubscription Subscribe(IServiceProvider services, Guid? projectId = null) {
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        return services.GetRequiredService<EditingChangeFeed>().Subscribe(new(
            new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.Project, projectId));
    }

    private static async Task<IReadOnlyList<EditingChange>> ReadAsync(IEditingChangeSubscription subscription) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        try {
            var batch = await subscription.ReadAsync(deadline.Token);
            Assert.False(batch.RequiresResync);
            return batch.Changes;
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
            return [];
        }
    }

    private static async Task<ProjectDefinitionOperation> OperationAsync(IServiceProvider services, ProjectDefinitionInvocation invocation) {
        var context = await services.GetRequiredService<ProjectDefinitionOperationProtocol>().GetWriteContextAsync(invocation.Caller);
        return new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding);
    }

    private static CreateProjectDefinitionCommand Creation() => new(
        new("Observed project", "Description", "Objective", default, "Planning", null), [], [], null);

    private sealed class CommitBarrier : DbTransactionInterceptor {
        public bool Armed { get; set; }
        public bool Rollback { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
                if (Rollback) {
                    throw new InvalidOperationException("Owned precommit rollback.");
                }
            }
            return result;
        }
    }

    private sealed class FailingActivity : IActivityStream {
        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default)
            => Task.FromException(new IOException("Owned optional postcommit activity failure."));
    }

    private sealed class RevokeAfterRootWrite : DbCommandInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("INSERT INTO \"Projects_Projects\"", StringComparison.Ordinal)) {
                Reached = true;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ReceiptAccess : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission project, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class RecordedContribution : IProjectCommittedChangeSink {
        public List<Guid> Projects { get; } = [];
        public void PublishCommitted(EditingScope scope, Guid projectId, EditingChangeKind kind) => Projects.Add(projectId);
    }
}
