using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDashboardActivityQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Persistent_store_prioritizes_nonterminal_runs_and_returns_a_bounded_deterministic_window()
    {
        await using var dbContext = CreateDbContext();
        var allStatuses = Enum.GetValues<ProcessRuntimeStatus>();
        var states = allStatuses.Select((status, index) =>
            CreateRuntimeState(index + 1, status, Now.AddMinutes(index + 1)));
        dbContext.RuntimeStates.AddRange(states);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var store = new EfProcessRuntimeUnitOfWork(dbContext);
        var expectedRunIds = allStatuses
            .Select((status, index) => new { Status = status, RunId = CreateProcessRunId(index + 1) })
            .Where(item => !ProcessRuntimeTerminalStates.IsRunTerminal(item.Status))
            .OrderByDescending(item => item.RunId.Value)
            .Take(ProcessRuntimeActivityQuery.MaximumTake)
            .Select(item => item.RunId)
            .ToArray();

        var result = await store.QueryActivityAsync(new ProcessRuntimeActivityQuery());

        Assert.Equal(ProcessRuntimeActivitySelectionMode.Active, result.Mode);
        Assert.Equal(5, result.Runs.Count);
        Assert.Equal(expectedRunIds, result.Runs.Select(run => run.RunId));
        Assert.All(result.Runs, run => Assert.False(ProcessRuntimeTerminalStates.IsRunTerminal(run.Status)));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public void Canonical_process_terminal_policy_is_exhaustive()
    {
        var terminalStatuses = Enum.GetValues<ProcessRuntimeStatus>()
            .Where(ProcessRuntimeTerminalStates.IsRunTerminal);

        Assert.Equal(
            [ProcessRuntimeStatus.Completed, ProcessRuntimeStatus.Failed, ProcessRuntimeStatus.Cancelled],
            terminalStatuses);
    }

    [Fact]
    public async Task Persistent_store_uses_run_id_as_the_equal_timestamp_tie_breaker()
    {
        await using var dbContext = CreateDbContext();
        dbContext.RuntimeStates.AddRange(
            CreateRuntimeState(1, ProcessRuntimeStatus.Active, Now),
            CreateRuntimeState(3, ProcessRuntimeStatus.Waiting, Now),
            CreateRuntimeState(2, ProcessRuntimeStatus.Blocked, Now));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var store = new EfProcessRuntimeUnitOfWork(dbContext);

        var result = await store.QueryActivityAsync(new ProcessRuntimeActivityQuery(2));

        Assert.Equal(ProcessRuntimeActivitySelectionMode.Active, result.Mode);
        Assert.Equal([3, 2], result.Runs.Select(run => RunSequence(run.RunId)));
    }

    [Fact]
    public async Task Persistent_store_falls_back_to_latest_terminal_runs_when_none_are_active()
    {
        await using var dbContext = CreateDbContext();
        dbContext.RuntimeStates.AddRange(
            CreateRuntimeState(1, ProcessRuntimeStatus.Completed, Now),
            CreateRuntimeState(2, ProcessRuntimeStatus.Failed, Now.AddMinutes(2)),
            CreateRuntimeState(3, ProcessRuntimeStatus.Cancelled, Now.AddMinutes(1)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var store = new EfProcessRuntimeUnitOfWork(dbContext);

        var result = await store.QueryActivityAsync(new ProcessRuntimeActivityQuery(2));

        Assert.Equal(ProcessRuntimeActivitySelectionMode.RecentFallback, result.Mode);
        Assert.Equal([2, 3], result.Runs.Select(run => RunSequence(run.RunId)));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Dashboard_query_does_not_omit_an_older_active_canonical_run_behind_501_terminal_projections()
    {
        var activeRun = CreateRuntimeActivityRow(
            900,
            ProcessRuntimeStatus.Active,
            Now.AddDays(-10));
        var runtimeStore = new StubRuntimeActivityStore(new ProcessRuntimeActivitySelection(
            ProcessRuntimeActivitySelectionMode.Active,
            [activeRun]));
        var projectionStore = new RecordingProjectionStore();
        for (var sequence = 1; sequence <= 501; sequence++)
        {
            projectionStore.Add(CreateProjectionSnapshot(
                CreateProcessRunId(sequence),
                ProcessProjectedRunStatus.Completed,
                Now.AddMinutes(sequence),
                $"Terminal {sequence}"));
        }

        projectionStore.Add(CreateProjectionSnapshot(
            activeRun.RunId,
            ProcessProjectedRunStatus.Completed,
            Now.AddDays(-11),
            "Stale projected name",
            backlogEventCount: 37));
        var service = new ProcessDashboardActivityQueryService(
            runtimeStore,
            projectionStore,
            ProcessProjectionJsonCodec.Default,
            new DashboardRunRecordStore());

        var result = await service.QueryAsync(new ProcessDashboardActivityQuery());

        Assert.Equal(ProcessDashboardActivityMode.Active, result.Mode);
        var item = Assert.Single(result.Items);
        Assert.Equal(activeRun.RunId, item.RunId);
        Assert.Equal(ProcessRuntimeStatus.Active, item.Status);
        Assert.NotNull(item.Projection);
        Assert.Equal(ProcessProjectedRunStatus.Completed, item.Projection.Status);
        Assert.Equal(37, item.Projection.Freshness.Lag.BacklogEventCount);
        Assert.Equal([ProcessRuntimeProjectionKeys.Live(activeRun.RunId)], projectionStore.LoadedKeys);
        Assert.Equal(1, projectionStore.LoadSnapshotsCallCount);
        Assert.Equal(0, projectionStore.ReadSnapshotsCallCount);
        Assert.Equal(ProcessDashboardActivityQuery.MaximumTake, Assert.Single(runtimeStore.Queries).Take);
    }

    [Fact]
    public async Task Dashboard_query_keeps_canonical_activity_when_the_optional_projection_is_missing()
    {
        var activeRun = CreateRuntimeActivityRow(1, ProcessRuntimeStatus.WaitingForUser, Now);
        var service = new ProcessDashboardActivityQueryService(
            new StubRuntimeActivityStore(new ProcessRuntimeActivitySelection(
                ProcessRuntimeActivitySelectionMode.Active,
                [activeRun])),
            new RecordingProjectionStore(),
            ProcessProjectionJsonCodec.Default,
            new DashboardRunRecordStore());

        var result = await service.QueryAsync(new ProcessDashboardActivityQuery());

        var item = Assert.Single(result.Items);
        Assert.Equal(ProcessRuntimeStatus.WaitingForUser, item.Status);
        Assert.Null(item.Projection);
    }

    [Fact]
    public async Task Dashboard_query_loads_selected_projections_in_one_bounded_batch()
    {
        var runs = Enumerable.Range(1, 3)
            .Select(sequence => CreateRuntimeActivityRow(
                sequence,
                ProcessRuntimeStatus.Active,
                Now.AddMinutes(sequence)))
            .ToArray();
        var projectionStore = new RecordingProjectionStore();
        foreach (var run in runs)
        {
            projectionStore.Add(CreateProjectionSnapshot(
                run.RunId,
                ProcessProjectedRunStatus.Active,
                run.UpdatedAtUtc,
                $"Process {RunSequence(run.RunId)}"));
        }

        var service = new ProcessDashboardActivityQueryService(
            new StubRuntimeActivityStore(new ProcessRuntimeActivitySelection(
                ProcessRuntimeActivitySelectionMode.Active,
                runs)),
            projectionStore,
            ProcessProjectionJsonCodec.Default,
            new DashboardRunRecordStore());

        var result = await service.QueryAsync(new ProcessDashboardActivityQuery(3));

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.NotNull(item.Projection));
        Assert.Equal(1, projectionStore.LoadSnapshotsCallCount);
        Assert.Equal(
            runs.Select(run => ProcessRuntimeProjectionKeys.Live(run.RunId)),
            projectionStore.LoadedKeys);
    }

    [Fact]
    public async Task Dashboard_recent_fallback_enriches_empty_projection_identity_from_compact_record()
    {
        var run = CreateRuntimeActivityRow(
            4,
            ProcessRuntimeStatus.Completed,
            Now);
        var projectId = Guid.NewGuid();
        var definitionId = ProcessDefinitionId.New();
        var projectionStore = new RecordingProjectionStore();
        projectionStore.Add(CreateProjectionSnapshot(
            run.RunId,
            ProcessProjectedRunStatus.Completed,
            run.UpdatedAtUtc,
            processName: string.Empty,
            projectName: string.Empty));
        var recordStore = new DashboardRunRecordStore
        {
            Page = new ProcessRunRecordPage(
                [CreateRecordSummary(run.RunId, projectId, definitionId)],
                null)
        };
        var service = new ProcessDashboardActivityQueryService(
            new StubRuntimeActivityStore(new ProcessRuntimeActivitySelection(
                ProcessRuntimeActivitySelectionMode.RecentFallback,
                [run])),
            projectionStore,
            ProcessProjectionJsonCodec.Default,
            recordStore);

        var result = await service.QueryAsync(new ProcessDashboardActivityQuery());

        var projection = Assert.Single(result.Items).Projection;
        Assert.NotNull(projection);
        Assert.Equal(projectId, projection.ProjectId);
        Assert.Equal($"Project {projectId:D}", projection.ProjectName);
        Assert.Equal($"Process definition {definitionId}", projection.ProcessName);
        var query = Assert.Single(recordStore.Queries);
        Assert.Equal(ProcessRunRecordListPayload.Compact, query.Payload);
        Assert.Equal([run.RunId], query.RunIds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProcessDashboardActivityQuery.MaximumTake + 1)]
    public void Dashboard_query_rejects_unbounded_take(int take)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessDashboardActivityQuery(take));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessRuntimeActivityQuery(take));
    }

    [Fact]
    public void Process_activity_query_and_canonical_store_are_registered_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddProcessesModule(new ConfigurationBuilder().Build());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProcessDashboardActivityQueryService) &&
                descriptor.ImplementationType == typeof(ProcessDashboardActivityQueryService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProcessRuntimeActivityStore) &&
                descriptor.ImplementationFactory is not null &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(EfProcessRuntimeUnitOfWork));
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-dashboard-activity-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static ProcessRuntimeStateEntity CreateRuntimeState(
        int sequence,
        ProcessRuntimeStatus status,
        DateTimeOffset updatedAtUtc)
    {
        var runId = CreateProcessRunId(sequence);
        return new ProcessRuntimeStateEntity
        {
            RunId = runId.Value,
            RootRunId = runId.Value,
            PlanId = new Guid($"10000000-0000-0000-0000-{sequence:D12}"),
            PlanHash = $"plan-{sequence}",
            Status = status,
            UpdatedAtUtc = updatedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private static ProcessRuntimeActivityRow CreateRuntimeActivityRow(
        int sequence,
        ProcessRuntimeStatus status,
        DateTimeOffset updatedAtUtc)
    {
        var runId = CreateProcessRunId(sequence);
        return new ProcessRuntimeActivityRow(runId, runId, status, updatedAtUtc);
    }

    private static ProcessProjectionSnapshot CreateProjectionSnapshot(
        ProcessRunId runId,
        ProcessProjectedRunStatus status,
        DateTimeOffset updatedAtUtc,
        string processName,
        int backlogEventCount = 0,
        string projectName = "Project")
    {
        var freshness = new ProcessProjectionFreshness(
            updatedAtUtc,
            SourceGlobalSequence: 10,
            new ProcessProjectionLag(
                LatestKnownGlobalSequence: 10 + backlogEventCount,
                LastProcessedGlobalSequence: 10,
                BacklogEventCount: backlogEventCount));
        var projection = new ProcessLiveProcessSnapshot(
            runId,
            runId,
            status,
            IsActive: status is ProcessProjectedRunStatus.Active or ProcessProjectedRunStatus.NeedsAttention,
            updatedAtUtc.AddMinutes(-1),
            updatedAtUtc,
            freshness,
            [],
            [])
        {
            ProcessName = processName,
            ProjectName = projectName
        };
        return ProcessProjectionJsonCodec.Default.CreateSnapshot(
            ProcessRuntimeProjectionProjector.ProjectorName,
            ProcessRuntimeProjectionKeys.Live(runId),
            projection,
            updatedAtUtc);
    }

    private static ProcessRunId CreateProcessRunId(int sequence)
        => new(new Guid($"00000000-0000-0000-0000-{sequence:D12}"));

    private static int RunSequence(ProcessRunId runId)
        => int.Parse(runId.Value.ToString("N")[^12..]);

    private static ProcessRunRecordSummary CreateRecordSummary(
        ProcessRunId runId,
        Guid projectId,
        ProcessDefinitionId definitionId)
    {
        return new ProcessRunRecordSummary(
            new ProcessRunRecordIdentity(
                runId,
                runId,
                null,
                null,
                definitionId,
                null,
                projectId),
            ProcessRunDisposition.Succeeded,
            ProcessRunRecordLifecycleState.Current,
            ProcessRunRecordCompleteness.Complete,
            ProcessRunEvidenceSource.All,
            ProcessRunEvidenceSource.None,
            [],
            ProcessRunFactsStatus.Completed,
            1,
            null,
            null,
            null,
            ProcessRunNarrativeStatus.Completed,
            1,
            null,
            null,
            null,
            new ProcessRunRecordMetrics(
                Now.AddMinutes(-1),
                Now,
                60_000,
                1,
                1,
                1,
                0,
                0,
                0,
                1,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0),
            [],
            null,
            1,
            1,
            ProcessRunRecordSchema.CurrentVersion,
            Now);
    }

    private sealed class StubRuntimeActivityStore(
        ProcessRuntimeActivitySelection selection) : IProcessRuntimeActivityStore
    {
        public List<ProcessRuntimeActivityQuery> Queries { get; } = [];

        public Task<ProcessRuntimeActivitySelection> QueryActivityAsync(
            ProcessRuntimeActivityQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(selection);
        }
    }

    private sealed class DashboardRunRecordStore : IProcessRunRecordStore
    {
        public ProcessRunRecordPage Page { get; init; } = new([], null);

        public List<ProcessRunRecordListQuery> Queries { get; } = [];

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(Page);
        }

        public Task<ProcessRunRecord?> GetAsync(
            ProcessRunId runId,
            bool includeSuperseded = false,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
            ProcessRunRecordAnalyticsQuery query,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<bool> UpsertSeedAsync(
            ProcessRunRecordSeed seed,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<bool> SupersedeAsync(
            ProcessRunRecordSupersession supersession,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<bool> CompleteFactsAsync(
            ProcessRunFactsCompletion completion,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<bool> FailFactsAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<bool> CompleteNarrativeAsync(
            ProcessRunNarrativeCompletion completion,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<bool> FailNarrativeAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall()
            => new("Dashboard activity query must use only compact run-record list reads.");
    }

    private sealed class RecordingProjectionStore : IProcessProjectionStore
    {
        private readonly Dictionary<ProcessProjectionKey, ProcessProjectionSnapshot> snapshots = [];

        public List<ProcessProjectionKey> LoadedKeys { get; } = [];

        public int LoadSnapshotsCallCount { get; private set; }

        public int ReadSnapshotsCallCount { get; private set; }

        public void Add(ProcessProjectionSnapshot snapshot)
        {
            snapshots[snapshot.ProjectionKey] = snapshot;
        }

        public Task<ProcessProjectionSnapshot?> LoadSnapshotAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKey projectionKey,
            CancellationToken cancellationToken = default)
        {
            LoadedKeys.Add(projectionKey);
            snapshots.TryGetValue(projectionKey, out var snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<IReadOnlyList<ProcessProjectionSnapshot>> LoadSnapshotsAsync(
            ProcessProjectorName projectorName,
            IReadOnlyList<ProcessProjectionKey> projectionKeys,
            CancellationToken cancellationToken = default)
        {
            LoadSnapshotsCallCount++;
            LoadedKeys.AddRange(projectionKeys);
            return Task.FromResult<IReadOnlyList<ProcessProjectionSnapshot>>(
                projectionKeys
                    .Distinct()
                    .Where(snapshots.ContainsKey)
                    .Select(key => snapshots[key])
                    .ToArray());
        }

        public Task<IReadOnlyList<ProcessProjectionSnapshot>> ReadSnapshotsAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKeyPrefix projectionKeyPrefix,
            int take,
            CancellationToken cancellationToken = default)
        {
            ReadSnapshotsCallCount++;
            return Task.FromResult<IReadOnlyList<ProcessProjectionSnapshot>>(
                snapshots.Values
                    .OrderByDescending(snapshot => snapshot.UpdatedAtUtc)
                    .Take(take)
                    .ToArray());
        }

        public Task UpsertSnapshotAsync(
            ProcessProjectionSnapshot snapshot,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AppendHistoryAsync(
            ProcessProjectionHistoryRecord history,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessProjectionHistoryRecord>> ReadHistoryAsync(
            ProcessProjectionHistoryQuery query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveOffsetAsync(
            ProcessProjectorOffset offset,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProcessProjectorOffset?> LoadOffsetAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task WriteDeadLetterAsync(
            ProcessProjectionDeadLetter deadLetter,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessProjectionDeadLetter>> ReadDeadLettersAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            int take,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
