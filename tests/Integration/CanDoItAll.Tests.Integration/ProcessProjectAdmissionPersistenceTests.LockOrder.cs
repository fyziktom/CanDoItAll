using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessProjectAdmissionPersistenceTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Initial_Project_wait_leaves_root_available_and_cancellation_rolls_back(bool cancel, bool descendant) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var request = await InitialForLockRaceAsync(services, admission, descendant);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var publisherCoordinator = Coordinator(services);
        await using var publisher = Context(services);
        await using var transaction = await publisher.Database.BeginTransactionAsync(deadline.Token);
        using var participation = publisherCoordinator.Enter(publisher);
        await Policy(services, publisherCoordinator).AcquireMutationGatesAsync([admission], deadline.Token);
        var wait = new LockWaitProbe();
        var coordinator = Coordinator(services);
        await using var owner = Context(services);
        var unit = new EfProcessRuntimeUnitOfWork(owner, new ProjectGateClock(), coordinatedTransaction: coordinator,
            projectAdmissionPolicy: Policy(services, coordinator, wait));
        var commit = unit.CommitAsync(request, cancellation.Token);
        try {
            var pid = await wait.BackendPid.Task.WaitAsync(deadline.Token);
            await WaitForAdvisoryWaitAsync(services, pid, deadline.Token);
            Assert.False(commit.IsCompleted);
            await LockPublicationRootAsync(publisher, request.OriginalState.RootRunId.Value, deadline.Token);
            if (cancel) {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => commit).WaitAsync(deadline.Token);
                await using var observer = Context(services);
                await AssertRowsAsync(observer, request, expected: 0);
            }
            await transaction.CommitAsync(deadline.Token);
            if (!cancel) {
                Assert.True((await commit.WaitAsync(deadline.Token)).Succeeded);
            }
        } finally {
            cancellation.Cancel();
            await transaction.DisposeAsync();
            await commit.ContinueWith(_ => { }, TaskScheduler.Default);
        }
        if (cancel) {
            Assert.True((await unit.CommitAsync(request, deadline.Token)).Succeeded);
        }
        await AssertRowsAsync(owner, request, expected: 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initial_Process_admission_holds_Project_before_attempting_root(bool descendant) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var request = await InitialForLockRaceAsync(services, admission, descendant);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var pause = new BeforeRootPause();
        var coordinator = Coordinator(services);
        await using var owner = Context(services, pause);
        var unit = new EfProcessRuntimeUnitOfWork(owner, new ProjectGateClock(), coordinatedTransaction: coordinator,
            projectAdmissionPolicy: Policy(services, coordinator));
        var commit = unit.CommitAsync(request, deadline.Token);
        var publisherCoordinator = Coordinator(services);
        await using var publisher = Context(services);
        await using var transaction = await publisher.Database.BeginTransactionAsync(deadline.Token);
        using var participation = publisherCoordinator.Enter(publisher);
        Task? publicationGate = null;
        try {
            await pause.Entered.Task.WaitAsync(deadline.Token);
            var wait = new LockWaitProbe();
            publicationGate = Policy(services, publisherCoordinator, wait).AcquireMutationGatesAsync([admission], deadline.Token);
            await WaitForAdvisoryWaitAsync(services, await wait.BackendPid.Task.WaitAsync(deadline.Token), deadline.Token);
            Assert.False(publicationGate.IsCompleted);
            pause.Release.TrySetResult(true);
            Assert.True((await commit.WaitAsync(deadline.Token)).Succeeded);
            await publicationGate.WaitAsync(deadline.Token);
            await LockPublicationRootAsync(publisher, request.OriginalState.RootRunId.Value, deadline.Token);
            await transaction.CommitAsync(deadline.Token);
        } finally {
            pause.Release.TrySetResult(true);
            deadline.Cancel();
            await commit.ContinueWith(_ => { }, TaskScheduler.Default);
            if (publicationGate is not null) {
                await publicationGate.ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
        await AssertRowsAsync(owner, request, expected: 1);
    }

    [Fact]
    public async Task Lock_only_Project_gates_order_distinct_keys_without_revalidating_retired_lifetimes() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var retired = await CreateProjectAsync(services);
        var current = await CreateProjectAsync(services);
        await new ProjectLifecycleTestDriver(services).DeleteAsync(retired.ProjectId);
        var coordinator = Coordinator(services);
        await using var owner = Context(services);
        await using var transaction = await owner.Database.BeginTransactionAsync();
        using var participation = coordinator.Enter(owner);
        var probe = new ProjectGateKeys();
        var policy = Policy(services, coordinator, probe);
        await policy.AcquireMutationGatesAsync([current, retired, current]);
        var expected = new[] { retired, current }.Select(project => ProjectMutationScopeKeys.ForProject(project.ProjectId))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, probe.Keys);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => policy.RequireForMutationAsync(retired));
    }

    private static async Task<ProcessRuntimeCommitRequest> InitialForLockRaceAsync(IServiceProvider services,
        ProcessProjectAdmission admission, bool descendant) {
        var initial = ProcessProjectAdmissionFixture.Initial(admission);
        if (!descendant) {
            return initial;
        }
        var coordinator = Coordinator(services);
        await using var context = Context(services);
        var unit = new EfProcessRuntimeUnitOfWork(context, new ProjectGateClock(), coordinatedTransaction: coordinator,
            projectAdmissionPolicy: Policy(services, coordinator));
        var parent = await unit.CommitAsync(ProcessProjectAdmissionFixture.Initial(admission));
        Assert.True(parent.Succeeded);
        parent = await unit.CommitAsync(ProcessDecisionRunScopeFixture.Claim(parent.State));
        Assert.True(parent.Succeeded);
        var parentStep = parent.State.Steps.Single().StepInstanceId;
        var state = initial.OriginalState with { RootRunId = parent.State.RootRunId };
        return initial with {
            OriginalState = state,
            Mutation = initial.Mutation with { State = state,
                Events = initial.Mutation.Events.Select(item => item with { RootRunId = state.RootRunId }).ToArray() },
            ParentStepPrecondition = new(parent.State.RunId, parentStep),
            InitialAssignments = initial.InitialAssignments!.Select(item => item with {
                LaunchVariables = new Dictionary<string, string> {
                    [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parent.State.RunId.Value.ToString("D"),
                    [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStep.Value.ToString("D")
                }
            }).ToArray()
        };
    }

    private sealed class ProjectGateClock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now;
    }

    private static Task<int> LockPublicationRootAsync(ProcessPersistenceDbContext context, Guid rootId, CancellationToken cancellationToken) {
        var bytes = rootId.ToByteArray();
        var key = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(0, 8)) ^ BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8, 8));
        return context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }

    private sealed class BeforeRootPause : DbCommandInterceptor {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) &&
                    !command.CommandText.Contains("hashtextextended", StringComparison.Ordinal)) {
                Entered.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class ProjectGateKeys : DbCommandInterceptor {
        public ConcurrentQueue<string> Keys { get; } = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("hashtextextended", StringComparison.Ordinal)) {
                Keys.Enqueue(Assert.IsType<string>(Assert.Single(command.Parameters.Cast<DbParameter>()).Value));
            }
            return ValueTask.FromResult(result);
        }
    }
}
