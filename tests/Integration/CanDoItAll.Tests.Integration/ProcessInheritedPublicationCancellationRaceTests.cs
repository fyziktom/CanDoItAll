using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessInheritedPublicationCancellationRaceTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Enrolled_child_held_before_initial_publication_keeps_recovery_pending_then_settles_original_work(ReadBasisMode mode) {
        var clock = new ProjectTaskPlanScenarioClock();
        await using var entry = await ProjectTaskPlanExecutionEntryTests.EntryFixture.CreateAsync(configureServices: services =>
            services.AddSingleton<TimeProvider>(clock));
        await using var process = await ProcessStepMaterialFixture.CreateAsync(mode, existingSql: entry.Files.Sql,
            executorId: entry.Files.Agent.Id, allowTaskPublication: true);
        var services = entry.Files.Sql.Services;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var (parent, request) = await ProjectTaskPlanReviewFixture.PrepareRequestAsync(entry, process, deadline.Token);
        var requireParent = request.RequireOriginalParent;
        var parentChecks = 0;
        clock.Freeze();
        request = request with { RequireOriginalParent = async (run, token) => {
            await requireParent(run, token);
            clock.Advance(TimeSpan.FromSeconds(1));
            Interlocked.Increment(ref parentChecks);
        } };
        var root = services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var storageScope = WorkspaceScopeDescriptor.Organization(entry.Files.Sql.Project.DatabaseProfileId.ToString("N"));
        var layout = new FileSandboxWorkspaceStorageLayout(root, storageScope);
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var candidate = entry.Files.Original with { Id = request.ExecutionRunId, ChatSessionId = null };
        var blocker = await writer.AcquireCoordinationAsync(layout.RootPath,
            Assert.Single(FileSandboxWorkspaceWriteSet.Run(null, candidate, readsAgent: false).LockPaths(layout)),
            TimeSpan.FromSeconds(5), false, deadline.Token);
        Task<AgentInheritedPlanningWorkspaceResult>? execution = null;
        var observed = false;
        try {
            using var wait = new ProcessAdmissionCancellationRaceTests.PublicationWait(() => Volatile.Read(ref parentChecks) == 1);
            execution = services.GetRequiredService<IAgentInheritedPlanningWorkspaceService>()
                .ExecuteInheritedPlanningAsync(request, deadline.Token);
            if (await Task.WhenAny(wait.Entered.Task, execution).WaitAsync(deadline.Token) == execution) {
                await execution;
                Assert.Fail("The actual inherited producer never reached initial publication.");
            }
            await wait.Entered.Task.WaitAsync(deadline.Token);
            Assert.Equal(1, Volatile.Read(ref parentChecks));
            await using (await writer.AcquireCoordinationAsync(layout.RootPath, layout.WorkspaceLockPath,
                TimeSpan.FromSeconds(5), false, deadline.Token)) {
                Assert.False(execution.IsCompleted);
            }
            var fresh = new FileSandboxWorkspaceStore(root, storageScope);
            Assert.Null(await fresh.GetExecutionRunAsync(request.ExecutionRunId, deadline.Token));
            clock.Advance(TimeSpan.FromSeconds(1));
            var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(process.State.RunId, deadline.Token))!;
            await ProcessRootCancellationFixture.StopAsync(services.GetRequiredService<IProcessRuntimeUnitOfWork>(), state);
            var childOwner = services.GetServices<IProcessDecisionChildStopper>().Single(item => item.Contract == parent.ExecutionContract);
            Assert.Equal(ProcessDecisionChildStopResult.MissingJournal, await childOwner.StopAsync(parent, deadline.Token));
            var pending = await RecoverCycleAsync(services, deadline.Token);
            Assert.Contains(ProcessCancellationRecoveryPending.MissingJournal, pending.Observed);
            Assert.NotEqual(ProcessCancellationRecoveryPending.None, pending.Checkpoint.Pending);
            Assert.Equal(ProcessCancellationRecoveryStage.Runtime, pending.Checkpoint.Position.Stage);
            await using var database = Context(services);
            Assert.False((await database.CancellationRecoveries.AsNoTracking().SingleAsync(deadline.Token)).Completed);
            Assert.Equal(ProcessRuntimeStatus.CancelRequested, (await database.RuntimeStates.AsNoTracking().SingleAsync(deadline.Token)).Status);
            Assert.Null((await database.DecisionChildren.AsNoTracking().SingleAsync(deadline.Token)).OwnerRevision);
            Assert.False(execution.IsCompleted);
            Assert.Empty(entry.Wire.Requests);
            await blocker.DisposeAsync();
            blocker = null;
            var failure = await Record.ExceptionAsync(async () => await execution);
            observed = true;
            Assert.NotNull(failure);
            Assert.Empty(entry.Wire.Requests);
            var original = (await fresh.GetExecutionRunAsync(request.ExecutionRunId, deadline.Token))!;
            Assert.NotNull(original);
            var decision = original.ToolAdmission!.PlanningDecision!;
            Assert.Equal(request.Start.Allocation, decision.InheritedOrigin!.Allocation);
            Assert.Equal(mode, decision.InheritedOrigin.ParentPolicy.ReadBasisMode);
            Assert.Equal(TimeSpan.FromSeconds(1).Ticks, decision.Work.ClosedWorkTicks);
            Assert.Null(decision.Work.Open);
            var completed = await RecoverCycleAsync(services, deadline.Token);
            Assert.Equal(ProcessCancellationRecoveryStage.Completed, completed.Checkpoint.Position.Stage);
            Assert.Equal(ProcessCancellationRecoveryPending.None, completed.Checkpoint.Pending);
            var saved = (await fresh.GetExecutionRunAsync(request.ExecutionRunId, deadline.Token))!;
            Assert.Equal(RunOutcome.Cancelled, saved.Outcome);
            Assert.Equal(original.ToolAdmission.Session, saved.ToolAdmission!.Session);
            Assert.Equal(decision.Work with { Revision = checked(decision.Work.Revision + 1) }, saved.ToolAdmission.PlanningDecision!.Work);
            Assert.Equal(ProcessRuntimeStatus.Cancelled, (await database.RuntimeStates.AsNoTracking().SingleAsync(deadline.Token)).Status);
            Assert.True((await database.CancellationRecoveries.AsNoTracking().SingleAsync(deadline.Token)).Completed);
            var child = await database.DecisionChildren.AsNoTracking().SingleAsync(deadline.Token);
            var allocation = await database.DecisionBudgetAllocations.AsNoTracking().SingleAsync(item =>
                item.BudgetId == child.BudgetId && item.AllocationId == child.AllocationId, deadline.Token);
            Assert.Equal(decision.Work.ClosedWorkTicks, allocation.ConsumedWorkTicks);
            Assert.NotNull(child.OwnerRevision);
            Assert.Equal(child.OwnerRevision, allocation.OwnerWorkRevision);
            Assert.Equal(0, (await new EfProcessDecisionBudgetStore(database).ReadAsync(state.RunId, null, deadline.Token))!.ReservedWorkTicks);
            if (mode == ReadBasisMode.RunSnapshot) {
                Assert.Equal(ProcessDecisionRunScopePhase.Cancelled, (await database.DecisionRunScopes.AsNoTracking().SingleAsync(deadline.Token)).Phase);
            }
            Assert.Empty(entry.Wire.Requests);
            await entry.Files.Sql.VerifyAsync(0, 0);
        } finally {
            await deadline.CancelAsync();
            if (blocker is not null) {
                await blocker.DisposeAsync();
            }
            if (execution is not null && !observed) {
                try {
                    await execution;
                } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                }
            }
        }
    }

    private static async Task<(ProcessCancellationRecoveryCheckpoint Checkpoint, HashSet<ProcessCancellationRecoveryPending> Observed)>
        RecoverCycleAsync(IServiceProvider services, CancellationToken cancellationToken) {
        await using var queueScope = services.CreateAsyncScope();
        var queue = queueScope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryQueue>();
        ProcessCancellationRecoveryClaim? claim;
        using var due = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        due.CancelAfter(TimeSpan.FromSeconds(10));
        while ((claim = await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, TimeSpan.FromMinutes(1), due.Token)) is null) {
            await Task.Delay(25, due.Token);
        }
        HashSet<ProcessCancellationRecoveryPending> observed = [];
        for (var operation = 0; operation < 48; operation++) {
            await using var scope = services.CreateAsyncScope();
            var checkpoint = await scope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryStep>().AdvanceAsync(claim, cancellationToken);
            checkpoint.Validate(claim);
            observed.Add(checkpoint.Pending);
            var saved = await queue.CheckpointAsync(claim, checkpoint, cancellationToken);
            if (saved.Status == ProcessCancellationCheckpointStatus.Released) {
                return (checkpoint, observed);
            }
            Assert.Equal(ProcessCancellationCheckpointStatus.Applied, saved.Status);
            claim = saved.Claim!;
        }
        throw new InvalidOperationException("The original root did not complete one finite recovery cycle.");
    }
}
