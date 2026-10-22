using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessAdmissionCancellationRaceTests {
    [Fact]
    public async Task Original_admission_waiting_for_publication_cannot_escape_completed_root_cancellation() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true, persistExecution: false);
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        var journal = new AgentToolAdmissionJournal(store, fixture.Run.ToolAdmission!.Session.Profile,
            services.GetRequiredService<TimeProvider>(), services.GetServices<IAgentToolBackgroundSourcePolicy>());
        var reservation = await journal.PrepareBackgroundReservationAsync(fixture.Run);
        var layout = new FileSandboxWorkspaceStorageLayout(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(fixture.Run.ToolAdmission.Session.Profile.ProfileId.ToString("N")));
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var blocker = await writer.AcquireCoordinationAsync(layout.RootPath,
            FileSandboxWorkspaceWriteSet.Run(null, fixture.Run, readsAgent: true).LockPaths(layout)[0],
            TimeSpan.FromSeconds(5), false, deadline.Token);
        Task<ExecutionRunSourceReservationResult>? publication = null;
        var observed = false;
        try {
            using var wait = new PublicationWait();
            publication = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunReservationStore>(store).ReserveBackgroundExecutionRunAsync(
                ExecutionRunSourceKey.ForBackground(fixture.Run.SourceKind, fixture.Run.SourceId, fixture.Run.CorrelationId, fixture.Run.CausationId),
                new(fixture.Run, null, [], []), reservation, deadline.Token);
            await wait.Entered.Task.WaitAsync(deadline.Token);
            await using (await writer.AcquireCoordinationAsync(layout.RootPath, layout.WorkspaceLockPath,
                TimeSpan.FromSeconds(5), false, deadline.Token)) {
                Assert.False(publication.IsCompleted);
            }
            var owner = services.GetRequiredService<IProcessRuntimeUnitOfWork>();
            var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(new(fixture.RunId), deadline.Token))!;
            await ProcessRootCancellationFixture.StopAsync(owner, state);
            await CompleteRecoveryAsync(app.Services, deadline.Token);
            await using var database = new ProcessPersistenceDbContext(ProcessExecutionAuthorityPersistenceTests.Options(services));
            Assert.True((await database.CancellationRecoveries.AsNoTracking().SingleAsync(deadline.Token)).Completed);
            Assert.Equal(ProcessRuntimeStatus.Cancelled, (await database.RuntimeStates.AsNoTracking().SingleAsync(deadline.Token)).Status);
            await blocker.DisposeAsync();
            blocker = null;
            var failure = await Record.ExceptionAsync(async () => await publication);
            observed = true;
            Assert.IsType<ProcessExecutionAuthorityMismatchException>(failure);
            Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id, deadline.Token));
        } finally {
            await deadline.CancelAsync();
            if (blocker is not null) {
                await blocker.DisposeAsync();
            }
            if (publication is not null && !observed) {
                try {
                    await publication;
                } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                }
            }
        }
    }

    [Fact]
    public async Task Acquired_publication_fence_blocks_root_barrier_until_original_journal_is_visible() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true, persistExecution: false);
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        var journal = new AgentToolAdmissionJournal(store, fixture.Run.ToolAdmission!.Session.Profile,
            services.GetRequiredService<TimeProvider>(), services.GetServices<IAgentToolBackgroundSourcePolicy>());
        var reservation = await journal.PrepareBackgroundReservationAsync(fixture.Run);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var acquired = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = reservation.AcquirePublication;
        reservation = reservation with { AcquirePublication = async token => {
            var lease = await original(token);
            try {
                var context = Assert.IsType<ProcessPersistenceDbContext>(lease);
                acquired.SetResult(await context.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
                await release.Task.WaitAsync(token);
                return lease;
            } catch {
                await lease.DisposeAsync();
                throw;
            }
        } };
        var publication = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunReservationStore>(store).ReserveBackgroundExecutionRunAsync(
            ExecutionRunSourceKey.ForBackground(fixture.Run.SourceKind, fixture.Run.SourceId, fixture.Run.CorrelationId, fixture.Run.CausationId),
            new(fixture.Run, null, [], []), reservation, deadline.Token);
        Task<ProcessRuntimeStateSnapshot>? stopping = null;
        var publicationObserved = false;
        var stoppingObserved = false;
        try {
            var publisherPid = await acquired.Task.WaitAsync(deadline.Token);
            var owner = services.GetRequiredService<IProcessRuntimeUnitOfWork>();
            var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(new(fixture.RunId), deadline.Token))!;
            stopping = ProcessRootCancellationFixture.StopAsync(owner, state);
            await using var database = new ProcessPersistenceDbContext(ProcessExecutionAuthorityPersistenceTests.Options(services));
            var timer = Stopwatch.StartNew();
            while (!await database.Database.SqlQuery<bool>($"""
                    SELECT EXISTS (SELECT 1 FROM pg_locks WHERE NOT granted AND {publisherPid} = ANY(pg_blocking_pids(pid))) AS "Value"
                    """).SingleAsync(deadline.Token)) {
                Assert.False(stopping.IsCompleted);
                Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), "Root cancellation did not wait on the retained publication transaction.");
                await Task.Delay(25, deadline.Token);
            }
            Assert.False(stopping.IsCompleted);
            Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id, deadline.Token));
            release.SetResult();
            var created = await publication;
            publicationObserved = true;
            Assert.Equal(ExecutionRunSourceDisposition.Created, created.Disposition);
            await stopping;
            stoppingObserved = true;
            await CompleteRecoveryAsync(app.Services, deadline.Token);
            var saved = (await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id, deadline.Token))!;
            Assert.Equal(RunOutcome.Cancelled, saved.Outcome);
            Assert.Equal(fixture.Run.ToolAdmission.Session, saved.ToolAdmission!.Session);
            Assert.True((await database.CancellationRecoveries.AsNoTracking().SingleAsync(deadline.Token)).Completed);
        } finally {
            release.TrySetResult();
            await deadline.CancelAsync();
            try {
                if (!publicationObserved) {
                    try {
                        await publication;
                    } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                    }
                }
            } finally {
                if (stopping is not null && !stoppingObserved) {
                    await stopping;
                }
            }
        }
    }

    internal static async Task CompleteRecoveryAsync(IServiceProvider services, CancellationToken cancellationToken) {
        await using var scope = services.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryQueue>();
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, TimeSpan.FromMinutes(1), cancellationToken))!;
        for (var operation = 0; operation < 16; operation++) {
            await using var recoveryScope = services.CreateAsyncScope();
            var checkpoint = await recoveryScope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryStep>().AdvanceAsync(claim, cancellationToken);
            checkpoint.Validate(claim);
            Assert.Equal(ProcessCancellationRecoveryPending.None, checkpoint.Pending);
            var saved = await queue.CheckpointAsync(claim, checkpoint, cancellationToken);
            if (checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Completed) {
                Assert.Equal(ProcessCancellationCheckpointStatus.Released, saved.Status);
                return;
            }
            Assert.Equal(ProcessCancellationCheckpointStatus.Applied, saved.Status);
            claim = saved.Claim!;
        }
        Assert.Fail("Root recovery did not complete its bounded stage sequence.");
    }

    internal static TestHarnessOptions Harness(Action<IServiceCollection>? configure = null)
        => ProcessExecutionAuthorityPersistenceTests.Harness(configure: services => {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new Clock()));
            services.Replace(ServiceDescriptor.Singleton<IAgentExecutionProfileGenerationSource>(new Generation()));
            configure?.Invoke(services);
        });

    private sealed class Clock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now.AddSeconds(5);
    }

    private sealed class Generation : IAgentExecutionProfileGenerationSource {
        public DatabaseProfileGeneration GetGeneration() => new(1);
    }

    private sealed class PublicationWait : IDisposable {
        private readonly Activity root = new Activity("process-admission-publication").SetIdFormat(ActivityIdFormat.W3C).Start();
        private readonly ActivityListener listener;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal PublicationWait() {
            listener = new() {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.publication.keys.wait") {
                        Entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public void Dispose() {
            listener.Dispose();
            root.Dispose();
        }
    }
}
