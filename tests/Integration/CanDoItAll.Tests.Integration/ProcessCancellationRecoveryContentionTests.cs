using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessCancellationRecoveryContentionTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(ReadBasisMode.StepSnapshot, 1)]
    [InlineData(ReadBasisMode.StepSnapshot, 8)]
    [InlineData(ReadBasisMode.StepSnapshot, 32)]
    [InlineData(ReadBasisMode.RunSnapshot, 1)]
    [InlineData(ReadBasisMode.RunSnapshot, 8)]
    [InlineData(ReadBasisMode.RunSnapshot, 32)]
    public Task Native_worker_stops_a_sibling_under_workspace_load_and_settles_both_original_children(ReadBasisMode mode, int writers)
        => RunAsync(mode, writers);

    internal async Task RunAsync(ReadBasisMode mode, int writers, Action<IServiceCollection>? configureServices = null) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode,
            configureServices: services => {
                services.Configure<ProcessCancellationRecoveryOptions>(options => {
                    options.Enabled = true;
                    options.IdleDelay = TimeSpan.FromMilliseconds(25);
                    options.Limits = ProcessCancellationRecoverySliceLimits.Default with { LeaseDuration = TimeSpan.FromSeconds(5) };
                });
                configureServices?.Invoke(services);
            });
        await using var sibling = await ProcessStepMaterialFixture.CreateAsync(mode, existingSql: fixture.Entry.Files.Sql,
            executorId: fixture.Entry.Files.Agent.Id, allowTaskPublication: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        var (siblingBinding, siblingRun, siblingState) = await StartSiblingAsync(fixture, sibling, token);
        fixture.Entry.Wire.OnRequest = null;
        fixture.Clock.Resume();
        var services = fixture.Services;
        var store = fixture.Entry.Files.Store;
        var layout = new FileSandboxWorkspaceStorageLayout(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(fixture.Entry.Files.Sql.Project.DatabaseProfileId.ToString("N")));
        ExecutionRunRecord BackgroundRun() => fixture.Waiting with {
            Id = Guid.NewGuid(), ChatSessionId = null, ToolAdmission = null, PendingApprovals = [], MetadataJson = "{}",
            Title = "Competing publication", ProcessRunId = Guid.NewGuid().ToString("D"), ProcessStepId = Guid.NewGuid().ToString("D"),
            State = ExecutionState.Completed, Outcome = RunOutcome.Succeeded
        };
        var selected = Enumerable.Range(0, writers).Select(_ => BackgroundRun()).ToArray();
        var history = Enumerable.Range(0, 2000).Select(_ => BackgroundRun()).ToArray();
        var preparation = Stopwatch.StartNew();
        await ProcessExecutionHistoryFixture.SeedBeforeActorsStartAsync(layout, [.. selected, .. history], token);
        output.WriteLine($"History preparation: {history.Length} historical runs, {selected.Length} selected runs, {preparation.Elapsed.TotalMilliseconds:F1} ms before concurrent actors start.");
        var original = await File.ReadAllBytesAsync(layout.RunPath(fixture.Waiting.Id), token);
        var key = Assert.Single(FileSandboxWorkspaceWriteSet.Run(null, fixture.Waiting with { ChatSessionId = null }, readsAgent: false).LockPaths(layout));
        IAsyncDisposable? blocker = await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory())
            .AcquireCoordinationAsync(layout.RootPath, key, TimeSpan.FromSeconds(1), false, token);
        var worker = services.GetServices<IHostedService>().OfType<ProcessCancellationRecoveryWorker>().Single();
        var writes = new ConcurrentBag<double>();
        var conflicts = new ConcurrentBag<double>();
        var accepted = new int[writers];
        var next = -1;
        var finished = 0;
        var batch = Task.CompletedTask;
        var elapsed = new Stopwatch();
        var allocated = 0L;
        var siblingObservedDuringLoad = false;
        var finishedAtSiblingStop = 0;
        double? siblingStopMilliseconds = null;
        ProcessCancellationRecoveryEntity[] rows = [];
        ProcessCancellationRecoveryEntity[] beforeRelease = [];
        try {
            var engine = new ProcessRuntimeEngine(services.GetRequiredService<IProcessRuntimeUnitOfWork>());
            var firstContext = fixture.CommandContext;
            var secondContext = fixture.CommandContext;
            Assert.True((await engine.BeginRootCancellationAsync(fixture.State, firstContext, token)).CommittedNow);
            Assert.True((await engine.BeginRootCancellationAsync(siblingState, secondContext, token)).CommittedNow);
            await using var inspection = Context(services);
            await worker.StartAsync(token);
            while (!await inspection.CancellationRecoveries.AsNoTracking().AnyAsync(
                    item => item.RootRunId == fixture.State.RootRunId.Value && item.LeaseGeneration > 0, token)) {
                await Task.Delay(10, token);
            }
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            batch = Task.WhenAll(Enumerable.Range(0, writers).Select(async owner => {
                var publisher = new FileSandboxWorkspaceStore(layout.RootPath, layout.Scope);
                await start.Task.WaitAsync(token);
                while (Interlocked.Increment(ref next) < 100) {
                    var watch = Stopwatch.StartNew();
                    try {
                        await publisher.UpdateExecutionRunDetailAsync(selected[owner].Id, current => current with {
                            Run = current.Run with { Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = current.Run.UpdatedAtUtc.AddTicks(1) }
                        }, token);
                        accepted[owner]++;
                        writes.Add(watch.Elapsed.TotalMilliseconds);
                    } catch (SandboxWorkspacePublicationConflictException) {
                        conflicts.Add(watch.Elapsed.TotalMilliseconds);
                    } finally {
                        Interlocked.Increment(ref finished);
                    }
                }
            }));
            allocated = GC.GetTotalAllocatedBytes(precise: true);
            token.ThrowIfCancellationRequested();
            deadline.CancelAfter(TimeSpan.FromMinutes(3));
            elapsed.Start();
            start.SetResult();
            while (!batch.IsCompleted) {
                var observed = await store.GetExecutionRunAsync(siblingRun.Id, token);
                if (observed?.Outcome == RunOutcome.Cancelled) {
                    siblingStopMilliseconds = elapsed.Elapsed.TotalMilliseconds;
                    finishedAtSiblingStop = Volatile.Read(ref finished);
                    siblingObservedDuringLoad = finishedAtSiblingStop < 100;
                    break;
                }
                await Task.Delay(10, token);
            }
            Assert.Equal(original, await File.ReadAllBytesAsync(layout.RunPath(fixture.Waiting.Id), token));
            rows = await inspection.CancellationRecoveries.AsNoTracking().ToArrayAsync(token);
            beforeRelease = rows;
            Assert.False(rows.Single(item => item.RootRunId == fixture.State.RootRunId.Value).Completed);
            await blocker.DisposeAsync();
            blocker = null;
            await batch;
            elapsed.Stop();
            allocated = GC.GetTotalAllocatedBytes(precise: true) - allocated;
            Assert.Equal(100, writes.Count + conflicts.Count);
            for (var owner = 0; owner < writers; owner++) {
                Assert.Equal(selected[owner].Revision + accepted[owner], (await store.GetExecutionRunAsync(selected[owner].Id, token))!.Revision);
            }
            while (rows.Any(item => !item.Completed)) {
                await Task.Delay(25, token);
                rows = await inspection.CancellationRecoveries.AsNoTracking().ToArrayAsync(token);
            }
            await worker.StopAsync(default);
            Assert.Equal(2, rows.Length);
            Assert.Equal(firstContext.CommandId.Value, rows.Single(item => item.RootRunId == fixture.State.RootRunId.Value).BarrierCommandId);
            Assert.Equal(secondContext.CommandId.Value, rows.Single(item => item.RootRunId == siblingState.RootRunId.Value).BarrierCommandId);
            foreach (var (binding, waiting) in new[] { (fixture.Parent, fixture.Waiting), (siblingBinding, siblingRun) }) {
                var run = (await store.GetExecutionRunAsync(waiting.Id, token))!;
                Assert.Equal(RunOutcome.Cancelled, run.Outcome);
                Assert.Null(run.ToolAdmission!.PlanningDecision!.Work.Open);
                var work = run.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks;
                Assert.Equal(waiting.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks, work);
                var root = (await new EfProcessRuntimeUnitOfWork(inspection).LoadAsync(binding.Child.Address.RunId, token))!;
                Assert.Equal(ProcessRuntimeStatus.Cancelled, root.Status);
                Assert.Equal(ProcessDecisionPhase.Cancelled, (await new EfProcessDecisionStore(inspection)
                    .ReadAsync(root.RunId, binding.Child.Address.StepInstanceId, token))!.Phase);
                var childId = binding.Child.Address.ChildId.Value;
                var child = await inspection.DecisionChildren.AsNoTracking().SingleAsync(item => item.ChildId == childId, token);
                Assert.Equal(binding.Child.AllocationId.Value, child.AllocationId);
                Assert.Equal(binding.Child.BudgetId.Value, child.BudgetId);
                var allocation = await inspection.DecisionBudgetAllocations.AsNoTracking().SingleAsync(
                    item => item.BudgetId == child.BudgetId && item.AllocationId == child.AllocationId, token);
                Assert.Equal(work, allocation.ConsumedWorkTicks);
                Assert.NotNull(child.OwnerRevision);
                Assert.Equal(child.OwnerRevision, allocation.OwnerWorkRevision);
                Assert.Equal(0, (await new EfProcessDecisionBudgetStore(inspection).ReadAsync(root.RunId, null, token))!.ReservedWorkTicks);
            }
            if (mode == ReadBasisMode.RunSnapshot) {
                var scopes = await inspection.DecisionRunScopes.AsNoTracking().ToArrayAsync(token);
                Assert.Equal(2, scopes.Length);
                Assert.All(scopes, scope => Assert.Equal(ProcessDecisionRunScopePhase.Cancelled, scope.Phase));
            }
            var receipts = await inspection.IdempotencyKeys.CountAsync(token);
            await using var replay = services.CreateAsyncScope();
            Assert.Equal(ProcessCancellationRecoverySliceStatus.Idle, (await replay.ServiceProvider
                .GetRequiredService<ProcessCancellationRecoveryCoordinator>()
                .RunSliceAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, ProcessCancellationRecoverySliceLimits.Default, token)).Status);
            Assert.Equal(receipts, await inspection.IdempotencyKeys.CountAsync(token));
            Assert.Equal(2, fixture.Entry.Wire.Requests.Count);
            await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
            Assert.True(siblingObservedDuringLoad, "The healthy child did not durably cancel before the competing finite writer batch ended.");
        } finally {
            deadline.Cancel();
            try {
                if (blocker is not null) {
                    await blocker.DisposeAsync();
                }
                await worker.StopAsync(default);
                try {
                    await batch;
                } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                }
            } finally {
                output.WriteLine(JsonSerializer.Serialize(new {
                    mode, writers, historyCount = 2000, attemptedWrites = Volatile.Read(ref finished), acceptedWrites = writes.Count,
                    conflictingWrites = conflicts.Count, siblingObservedDuringLoad, finishedAtSiblingStop, siblingStopMilliseconds,
                    elapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds, allocatedBytes = elapsed.IsRunning ? (long?)null : allocated,
                    writeMilliseconds = Distribution(writes), conflictMilliseconds = Distribution(conflicts),
                    beforeRelease = beforeRelease.Select(row => new { row.RootRunId, row.Completed, row.Pending, row.LeaseGeneration, row.ProgressRevision, row.PositionJson }),
                    recovery = rows.Select(row => new { row.Completed, row.Pending, row.LeaseGeneration, row.ProgressRevision, row.PositionJson }),
                    scope = "Real children and registered worker; default two-second slice; five-second test lease; finite single-attempt load; cache uncontrolled."
                }));
            }
        }
    }

    private static async Task<(ProcessDecisionChildMaterialBinding Binding, ExecutionRunRecord Run, ProcessRuntimeStateSnapshot State)>
        StartSiblingAsync(ProjectTaskPlanReviewFixture fixture, ProcessStepMaterialFixture sibling, CancellationToken token) {
        var (binding, request) = await ProjectTaskPlanReviewFixture.PrepareRequestAsync(fixture.Entry, sibling, token);
        var services = fixture.Services;
        var run = (await services.GetRequiredService<IAgentInheritedPlanningWorkspaceService>().ExecuteInheritedPlanningAsync(request, token)).Run;
        Assert.True(run.State == ExecutionState.WaitingOnTool, run.ResultSummary);
        var claim = sibling.State.Claims.Single(item => item.ClaimToken == sibling.Claim.ClaimToken);
        var key = Guid.NewGuid();
        StrategyResultEnvelope result = new(new("contended-cancellation-fixture"), "1.0.0", key, StrategyOutcome.NeedsManager,
            [], [], [], [], "sha256:" + new string('e', 64)) { ExecutionRunId = new(run.Id) };
        var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(sibling.State.RunId, token))!;
        var blocked = await services.GetRequiredService<ProcessDecisionResultCoordinator>().SubmitAsync(state, fixture.CommandContext,
            new(binding.Child.Address.StepInstanceId, claim.OwnerId, claim.ClaimToken, new(key), result), token);
        Assert.True(blocked.Succeeded, string.Join("; ", blocked.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ProcessDecisionPhase.WaitingForReview, blocked.Decision!.Current.Phase);
        Assert.Equal(DispatchClaimStatus.Completed, blocked.State.Claims.Single().Status);
        return (binding, run, blocked.State);
    }

    private static object Distribution(IEnumerable<double> values) {
        var sorted = values.Order().ToArray();
        double? At(double percentile) => sorted.Length == 0 ? null : sorted[Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length) - 1)];
        return new { p50 = At(0.5), p95 = At(0.95), p99 = At(0.99) };
    }
}
