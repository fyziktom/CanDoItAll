using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceExecutionSliceStore(
    FileSandboxWorkspaceStorageLayout layout,
    FileSandboxWorkspaceJsonStore jsonStore)
{
    public bool ExecutionStorageExists() => layout.ExecutionStorageExists();

    public async Task<SandboxWorkspaceExecutionState> LoadAsync(CancellationToken cancellationToken)
    {
        if (ExecutionStorageExists())
        {
            return await LoadExecutionSlicesAsync(cancellationToken);
        }

        return await TryLoadLegacyExecutionStateAsync(cancellationToken)
            ?? SandboxWorkspaceExecutionState.Empty;
    }

    public Task<SandboxWorkspaceExecutionState?> TryLoadLegacyExecutionStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(layout.LegacyExecutionPath))
        {
            return Task.FromResult<SandboxWorkspaceExecutionState?>(null);
        }

        return jsonStore.ReadJsonAsync<SandboxWorkspaceExecutionState>(layout.LegacyExecutionPath, cancellationToken);
    }

    public async Task<ExecutionRunDetail?> LoadRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(layout.RunPath(executionRunId), cancellationToken);
        if (run is null)
        {
            return null;
        }

        var session = run.ChatSessionId.HasValue
            ? await jsonStore.ReadJsonAsync<ChatSessionRecord>(layout.SessionPath(run.ChatSessionId.Value), cancellationToken)
            : null;

        return new ExecutionRunDetail(
            Run: run,
            ChatSession: session,
            ExecutionLog: (await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(layout.RunLogsRoot(executionRunId), cancellationToken))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList(),
            Metrics: (await jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(layout.RunMetricsRoot(executionRunId), cancellationToken))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList())
        {
            Approvals = (await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionApprovalRecord>(layout.RunApprovalsRoot(executionRunId), cancellationToken))
                .OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc)
                .ToList(),
            Artifacts = (await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionArtifactRecord>(layout.RunArtifactsRoot(executionRunId), cancellationToken))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList(),
            Checkpoints = (await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionWorkflowCheckpointRecord>(layout.RunWorkflowCheckpointsRoot(executionRunId), cancellationToken))
                .OrderByDescending(item => item.CapturedAtUtc)
                .ToList(),
            ToolReceipts = (await jsonStore.LoadRecordsFromDirectoryAsync<ToolExecutionReceiptRecord>(layout.RunReceiptsRoot(executionRunId), cancellationToken))
                .OrderByDescending(item => item.CompletedAtUtc)
                .ToList()
        };
    }

    public async Task<ExecutionSliceSaveResult> SaveRunDetailAsync(
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var normalizedDetail = NormalizeRunDetail(detail);
        EnsureRunDetailConsistency(normalizedDetail);

        Directory.CreateDirectory(layout.ExecutionStorageRoot);

        var sessionExistedBefore = normalizedDetail.ChatSession is not null
            && File.Exists(layout.SessionPath(normalizedDetail.ChatSession.Id));
        var runExistedBefore = File.Exists(layout.RunPath(normalizedDetail.Run.Id));
        var changed = false;

        if (normalizedDetail.ChatSession is not null)
        {
            changed |= await jsonStore.WriteJsonIfChangedAsync(
                layout.SessionPath(normalizedDetail.ChatSession.Id),
                normalizedDetail.ChatSession,
                cancellationToken);
        }

        changed |= await PersistRunAsync(
            previousDetail?.Run,
            normalizedDetail.Run,
            previousDetail?.ExecutionLog ?? [],
            normalizedDetail.ExecutionLog,
            previousDetail?.Metrics ?? [],
            normalizedDetail.Metrics,
            previousDetail?.Approvals ?? [],
            normalizedDetail.Approvals,
            previousDetail?.Artifacts ?? [],
            normalizedDetail.Artifacts,
            previousDetail?.Checkpoints ?? [],
            normalizedDetail.Checkpoints,
            previousDetail?.ToolReceipts ?? [],
            normalizedDetail.ToolReceipts,
            cancellationToken);

        var currentIndex = await ResolveExecutionIndexAsync(cancellationToken);
        var nextIndex = new ExecutionStorageIndex(
            Version: string.IsNullOrWhiteSpace(currentIndex.Version) ? "3.0" : currentIndex.Version,
            Revision: changed ? currentIndex.Revision + 1L : currentIndex.Revision,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SessionCount: currentIndex.SessionCount + (!sessionExistedBefore && normalizedDetail.ChatSession is not null ? 1 : 0),
            RunCount: currentIndex.RunCount + (!runExistedBefore ? 1 : 0),
            LogCount: currentIndex.LogCount + normalizedDetail.ExecutionLog.Count - (previousDetail?.ExecutionLog.Count ?? 0),
            MetricCount: currentIndex.MetricCount + normalizedDetail.Metrics.Count - (previousDetail?.Metrics.Count ?? 0),
            ApprovalCount: currentIndex.ApprovalCount + normalizedDetail.Approvals.Count - (previousDetail?.Approvals.Count ?? 0),
            ArtifactCount: currentIndex.ArtifactCount + normalizedDetail.Artifacts.Count - (previousDetail?.Artifacts.Count ?? 0),
            CheckpointCount: currentIndex.CheckpointCount + normalizedDetail.Checkpoints.Count - (previousDetail?.Checkpoints.Count ?? 0),
            ReceiptCount: currentIndex.ReceiptCount + normalizedDetail.ToolReceipts.Count - (previousDetail?.ToolReceipts.Count ?? 0));

        if (changed || !File.Exists(layout.ExecutionIndexPath) || jsonStore.RequiresSave(currentIndex, nextIndex))
        {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionIndexPath, nextIndex, cancellationToken);
        }

        return new ExecutionSliceSaveResult(changed, nextIndex, normalizedDetail);
    }

    public Task<bool> SaveAsync(SandboxWorkspaceExecutionState executionState, CancellationToken cancellationToken)
        => SaveAsync(SandboxWorkspaceExecutionState.Empty, executionState, cancellationToken);

    public async Task<bool> SaveAsync(
        SandboxWorkspaceExecutionState previousState,
        SandboxWorkspaceExecutionState executionState,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(layout.ExecutionStorageRoot);

        var changed = false;
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.ExecutionSessionsRoot,
            previousState.ChatSessions,
            executionState.ChatSessions,
            item => $"{item.Id:N}.json",
            cancellationToken);

        var previousLogsByRun = GroupByRunId(previousState.ExecutionLog, item => item.ExecutionRunId);
        var logsByRun = GroupByRunId(executionState.ExecutionLog, item => item.ExecutionRunId);
        var previousMetricsByRun = GroupByRunId(previousState.Metrics, item => item.ExecutionRunId);
        var metricsByRun = GroupByRunId(executionState.Metrics, item => item.ExecutionRunId);
        var previousApprovalsByRun = GroupByRunId(previousState.ExecutionApprovals, item => item.ExecutionRunId);
        var approvalsByRun = GroupByRunId(executionState.ExecutionApprovals, item => item.ExecutionRunId);
        var previousArtifactsByRun = GroupByRunId(previousState.ExecutionArtifacts, item => item.ExecutionRunId);
        var artifactsByRun = GroupByRunId(executionState.ExecutionArtifacts, item => item.ExecutionRunId);
        var previousCheckpointsByRun = GroupByRunId(previousState.ExecutionWorkflowCheckpoints, item => item.ExecutionRunId);
        var checkpointsByRun = GroupByRunId(executionState.ExecutionWorkflowCheckpoints, item => item.ExecutionRunId);
        var previousReceiptsByRun = GroupByRunId(previousState.ToolExecutionReceipts, item => item.ExecutionRunId);
        var receiptsByRun = GroupByRunId(executionState.ToolExecutionReceipts, item => item.ExecutionRunId);
        var previousRunsById = previousState.ExecutionRuns.ToDictionary(item => item.Id);
        var runsById = executionState.ExecutionRuns.ToDictionary(item => item.Id);
        var changedRunIds = new HashSet<Guid>();

        foreach (var runId in previousRunsById.Keys.Union(runsById.Keys))
        {
            if (!previousRunsById.TryGetValue(runId, out var previousRun) ||
                !runsById.TryGetValue(runId, out var run) ||
                !EqualityComparer<ExecutionRunRecord>.Default.Equals(previousRun, run))
            {
                changedRunIds.Add(runId);
            }
        }

        AddChangedRunIds(changedRunIds, previousLogsByRun, logsByRun);
        AddChangedRunIds(changedRunIds, previousMetricsByRun, metricsByRun);
        AddChangedRunIds(changedRunIds, previousApprovalsByRun, approvalsByRun);
        AddChangedRunIds(changedRunIds, previousArtifactsByRun, artifactsByRun);
        AddChangedRunIds(changedRunIds, previousCheckpointsByRun, checkpointsByRun);
        AddChangedRunIds(changedRunIds, previousReceiptsByRun, receiptsByRun);

        foreach (var runId in changedRunIds.Where(item => item != Guid.Empty))
        {
            if (!runsById.TryGetValue(runId, out var run))
            {
                var runRoot = layout.RunRoot(runId);
                if (Directory.Exists(runRoot))
                {
                    Directory.Delete(runRoot, recursive: true);
                    changed = true;
                }

                continue;
            }

            previousRunsById.TryGetValue(runId, out var previousRun);
            changed |= await PersistRunAsync(
                previousRun,
                run,
                GetItemsForRun(previousLogsByRun, runId),
                GetItemsForRun(logsByRun, runId),
                GetItemsForRun(previousMetricsByRun, runId),
                GetItemsForRun(metricsByRun, runId),
                GetItemsForRun(previousApprovalsByRun, runId),
                GetItemsForRun(approvalsByRun, runId),
                GetItemsForRun(previousArtifactsByRun, runId),
                GetItemsForRun(artifactsByRun, runId),
                GetItemsForRun(previousCheckpointsByRun, runId),
                GetItemsForRun(checkpointsByRun, runId),
                GetItemsForRun(previousReceiptsByRun, runId),
                GetItemsForRun(receiptsByRun, runId),
                cancellationToken);
        }

        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.OrphanLogsRoot,
            GetItemsForRun(previousLogsByRun, Guid.Empty),
            logsByRun.TryGetValue(Guid.Empty, out var orphanLogs) ? orphanLogs : [],
            item => $"{item.Id:N}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.OrphanMetricsRoot,
            GetItemsForRun(previousMetricsByRun, Guid.Empty),
            metricsByRun.TryGetValue(Guid.Empty, out var orphanMetrics) ? orphanMetrics : [],
            item => $"{item.Id:N}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.OrphanApprovalsRoot,
            GetItemsForRun(previousApprovalsByRun, Guid.Empty),
            approvalsByRun.TryGetValue(Guid.Empty, out var orphanApprovals) ? orphanApprovals : [],
            item => $"{item.ExecutionRunId:N}-{jsonStore.NormalizeFileName(item.ApprovalId)}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.OrphanArtifactsRoot,
            GetItemsForRun(previousArtifactsByRun, Guid.Empty),
            artifactsByRun.TryGetValue(Guid.Empty, out var orphanArtifacts) ? orphanArtifacts : [],
            item => $"{item.Id:N}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.OrphanReceiptsRoot,
            GetItemsForRun(previousReceiptsByRun, Guid.Empty),
            receiptsByRun.TryGetValue(Guid.Empty, out var orphanReceipts) ? orphanReceipts : [],
            item => $"{item.Id:N}.json",
            cancellationToken);

        var currentIndex = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(layout.ExecutionIndexPath, cancellationToken);
        var nextIndex = new ExecutionStorageIndex(
            Version: executionState.Version,
            Revision: changed ? (currentIndex?.Revision ?? 0L) + 1L : currentIndex?.Revision ?? 1L,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SessionCount: executionState.ChatSessions.Count,
            RunCount: executionState.ExecutionRuns.Count,
            LogCount: executionState.ExecutionLog.Count,
            MetricCount: executionState.Metrics.Count,
            ApprovalCount: executionState.ExecutionApprovals.Count,
            ArtifactCount: executionState.ExecutionArtifacts.Count,
            CheckpointCount: executionState.ExecutionWorkflowCheckpoints.Count,
            ReceiptCount: executionState.ToolExecutionReceipts.Count);

        if (changed || currentIndex is null || jsonStore.RequiresSave(currentIndex, nextIndex))
        {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionIndexPath, nextIndex, cancellationToken);
        }

        var currentChatIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(layout.ExecutionChatIndexPath, cancellationToken);
        var projection = WorkspaceChatProjectionBuilder.Build(
            executionState.ChatSessions,
            executionState.ExecutionRuns,
            executionState.ExecutionLog);
        var nextChatIndex = new ExecutionChatIndex(
            Version: nextIndex.Version,
            Revision: nextIndex.Revision,
            UpdatedAtUtc: nextIndex.UpdatedAtUtc,
            SessionSummaries: projection.SessionSummaries,
            RunSummaries: projection.RunSummaries);

        if (changed || currentChatIndex is null || jsonStore.RequiresSave(currentChatIndex, nextChatIndex))
        {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionChatIndexPath, nextChatIndex, cancellationToken);
        }

        return changed;
    }

    private async Task<SandboxWorkspaceExecutionState> LoadExecutionSlicesAsync(CancellationToken cancellationToken)
    {
        var sessions = await jsonStore.LoadRecordsFromDirectoryAsync<ChatSessionRecord>(layout.ExecutionSessionsRoot, cancellationToken);
        var runs = new List<ExecutionRunRecord>();
        var executionLog = new List<ExecutionLogEntry>();
        var metrics = new List<AgentRunMetric>();
        var approvals = new List<ExecutionApprovalRecord>();
        var artifacts = new List<ExecutionArtifactRecord>();
        var checkpoints = new List<ExecutionWorkflowCheckpointRecord>();
        var receipts = new List<ToolExecutionReceiptRecord>();

        if (Directory.Exists(layout.ExecutionRunsRoot))
        {
            foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(Path.Combine(runDirectory, "run.json"), cancellationToken);
                if (run is null)
                {
                    continue;
                }

                runs.Add(run);
                executionLog.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(Path.Combine(runDirectory, "logs"), cancellationToken));
                metrics.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(Path.Combine(runDirectory, "metrics"), cancellationToken));
                approvals.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionApprovalRecord>(Path.Combine(runDirectory, "approvals"), cancellationToken));
                artifacts.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionArtifactRecord>(Path.Combine(runDirectory, "audit", "artifacts"), cancellationToken));
                checkpoints.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionWorkflowCheckpointRecord>(Path.Combine(runDirectory, "workflow-checkpoints", "records"), cancellationToken));
                receipts.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ToolExecutionReceiptRecord>(Path.Combine(runDirectory, "audit", "receipts"), cancellationToken));
            }
        }

        executionLog.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(layout.OrphanLogsRoot, cancellationToken));
        metrics.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(layout.OrphanMetricsRoot, cancellationToken));
        approvals.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionApprovalRecord>(layout.OrphanApprovalsRoot, cancellationToken));
        artifacts.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionArtifactRecord>(layout.OrphanArtifactsRoot, cancellationToken));
        receipts.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ToolExecutionReceiptRecord>(layout.OrphanReceiptsRoot, cancellationToken));

        var index = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(layout.ExecutionIndexPath, cancellationToken);
        var version = string.IsNullOrWhiteSpace(index?.Version)
            ? "1.0"
            : index.Version;

        return new SandboxWorkspaceExecutionState(
            Version: version,
            ChatSessions: sessions,
            ExecutionLog: executionLog,
            Metrics: metrics)
        {
            ExecutionRuns = runs,
            ExecutionApprovals = approvals,
            ExecutionArtifacts = artifacts,
            ExecutionWorkflowCheckpoints = checkpoints,
            ToolExecutionReceipts = receipts
        };
    }

    private async Task<bool> PersistRunAsync(
        ExecutionRunRecord? previousRun,
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionLogEntry> previousExecutionLog,
        IReadOnlyList<ExecutionLogEntry> executionLog,
        IReadOnlyList<AgentRunMetric> previousMetrics,
        IReadOnlyList<AgentRunMetric> metrics,
        IReadOnlyList<ExecutionApprovalRecord> previousApprovals,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        IReadOnlyList<ExecutionArtifactRecord> previousArtifacts,
        IReadOnlyList<ExecutionArtifactRecord> artifacts,
        IReadOnlyList<ExecutionWorkflowCheckpointRecord> previousCheckpoints,
        IReadOnlyList<ExecutionWorkflowCheckpointRecord> checkpoints,
        IReadOnlyList<ToolExecutionReceiptRecord> previousReceipts,
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(layout.RunRoot(run.Id));

        var changed = false;
        if (previousRun is null || !EqualityComparer<ExecutionRunRecord>.Default.Equals(previousRun, run) || !File.Exists(layout.RunPath(run.Id)))
        {
            changed |= await jsonStore.WriteJsonIfChangedAsync(layout.RunPath(run.Id), run, cancellationToken);
        }

        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.RunLogsRoot(run.Id),
            previousExecutionLog,
            executionLog,
            item => $"{item.Id:N}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.RunMetricsRoot(run.Id),
            previousMetrics,
            metrics,
            item => $"{item.Id:N}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.RunApprovalsRoot(run.Id),
            previousApprovals,
            approvals,
            item => $"{jsonStore.NormalizeFileName(item.ApprovalId)}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.RunArtifactsRoot(run.Id),
            previousArtifacts,
            artifacts,
            item => $"{item.Id:N}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.RunWorkflowCheckpointsRoot(run.Id),
            previousCheckpoints,
            checkpoints,
            item => $"{item.Id:N}.json",
            cancellationToken);
        changed |= await jsonStore.PersistRecordDirectoryDiffAsync(
            layout.RunReceiptsRoot(run.Id),
            previousReceipts,
            receipts,
            item => $"{item.Id:N}.json",
            cancellationToken);
        return changed;
    }

    private static Dictionary<Guid, IReadOnlyList<T>> GroupByRunId<T>(
        IEnumerable<T> items,
        Func<T, Guid> runIdSelector)
    {
        return items
            .GroupBy(runIdSelector)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<T>)group.ToList());
    }

    private static IReadOnlyList<T> GetItemsForRun<T>(
        IReadOnlyDictionary<Guid, IReadOnlyList<T>> itemsByRun,
        Guid runId)
    {
        return itemsByRun.TryGetValue(runId, out var items)
            ? items
            : [];
    }

    private static void AddChangedRunIds<T>(
        ISet<Guid> changedRunIds,
        IReadOnlyDictionary<Guid, IReadOnlyList<T>> previousByRun,
        IReadOnlyDictionary<Guid, IReadOnlyList<T>> currentByRun)
    {
        foreach (var runId in previousByRun.Keys.Union(currentByRun.Keys))
        {
            if (!previousByRun.TryGetValue(runId, out var previousItems) ||
                !currentByRun.TryGetValue(runId, out var currentItems) ||
                !previousItems.SequenceEqual(currentItems))
            {
                changedRunIds.Add(runId);
            }
        }
    }

    private async Task<ExecutionStorageIndex> ResolveExecutionIndexAsync(CancellationToken cancellationToken)
    {
        var existing = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(layout.ExecutionIndexPath, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return new ExecutionStorageIndex(
            Version: "3.0",
            Revision: 1L,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SessionCount: CountJsonFiles(layout.ExecutionSessionsRoot),
            RunCount: CountRunFiles(),
            LogCount: CountRunScopedJsonFiles(runId => layout.RunLogsRoot(runId)) + CountJsonFiles(layout.OrphanLogsRoot),
            MetricCount: CountRunScopedJsonFiles(runId => layout.RunMetricsRoot(runId)) + CountJsonFiles(layout.OrphanMetricsRoot),
            ApprovalCount: CountRunScopedJsonFiles(runId => layout.RunApprovalsRoot(runId)) + CountJsonFiles(layout.OrphanApprovalsRoot),
            ArtifactCount: CountRunScopedJsonFiles(runId => layout.RunArtifactsRoot(runId)) + CountJsonFiles(layout.OrphanArtifactsRoot),
            CheckpointCount: CountRunScopedJsonFiles(runId => layout.RunWorkflowCheckpointsRoot(runId)),
            ReceiptCount: CountRunScopedJsonFiles(runId => layout.RunReceiptsRoot(runId)) + CountJsonFiles(layout.OrphanReceiptsRoot));
    }

    private int CountRunFiles()
    {
        if (!Directory.Exists(layout.ExecutionRunsRoot))
        {
            return 0;
        }

        return Directory.EnumerateDirectories(layout.ExecutionRunsRoot)
            .Count(runDirectory => File.Exists(Path.Combine(runDirectory, "run.json")));
    }

    private int CountRunScopedJsonFiles(Func<Guid, string> pathSelector)
    {
        if (!Directory.Exists(layout.ExecutionRunsRoot))
        {
            return 0;
        }

        var total = 0;
        foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot))
        {
            if (!Guid.TryParse(Path.GetFileName(runDirectory), out var runId))
            {
                continue;
            }

            total += CountJsonFiles(pathSelector(runId));
        }

        return total;
    }

    private static int CountJsonFiles(string directoryPath)
    {
        return Directory.Exists(directoryPath)
            ? Directory.EnumerateFiles(directoryPath, "*.json").Count()
            : 0;
    }

    private static ExecutionRunDetail NormalizeRunDetail(ExecutionRunDetail detail)
    {
        return new ExecutionRunDetail(
            Run: detail.Run,
            ChatSession: detail.ChatSession,
            ExecutionLog: detail.ExecutionLog.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Metrics: detail.Metrics.OrderByDescending(item => item.CreatedAtUtc).ToList())
        {
            Approvals = detail.Approvals.OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc).ToList(),
            Artifacts = detail.Artifacts.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Checkpoints = detail.Checkpoints.OrderByDescending(item => item.CapturedAtUtc).ToList(),
            ToolReceipts = detail.ToolReceipts.OrderByDescending(item => item.CompletedAtUtc).ToList()
        };
    }

    private static void EnsureRunDetailConsistency(ExecutionRunDetail detail)
    {
        if (detail.Run.ChatSessionId.HasValue)
        {
            if (detail.ChatSession is null)
            {
                throw new InvalidOperationException(
                    $"Execution run '{detail.Run.Id:N}' requires a matching chat session when a chat session id is present.");
            }

            if (detail.ChatSession.Id != detail.Run.ChatSessionId.Value)
            {
                throw new InvalidOperationException(
                    $"Execution run '{detail.Run.Id:N}' references chat session '{detail.Run.ChatSessionId.Value:N}', but the supplied session was '{detail.ChatSession.Id:N}'.");
            }
        }
        else if (detail.ChatSession is not null)
        {
            throw new InvalidOperationException(
                $"Execution run '{detail.Run.Id:N}' cannot persist a chat session when the run is not chat-backed.");
        }

        EnsureRunScoped(detail.Run.Id, detail.ExecutionLog.Select(item => item.ExecutionRunId), "execution log entry");
        EnsureRunScoped(detail.Run.Id, detail.Metrics.Select(item => item.ExecutionRunId), "execution metric");
        EnsureRunScoped(detail.Run.Id, detail.Approvals.Select(item => item.ExecutionRunId), "execution approval");
        EnsureRunScoped(detail.Run.Id, detail.Artifacts.Select(item => item.ExecutionRunId), "execution artifact");
        EnsureRunScoped(detail.Run.Id, detail.Checkpoints.Select(item => item.ExecutionRunId), "execution checkpoint");
        EnsureRunScoped(detail.Run.Id, detail.ToolReceipts.Select(item => item.ExecutionRunId), "tool execution receipt");
    }

    private static void EnsureRunScoped(
        Guid executionRunId,
        IEnumerable<Guid> relatedRunIds,
        string label)
    {
        if (relatedRunIds.Any(relatedRunId => relatedRunId != executionRunId))
        {
            throw new InvalidOperationException(
                $"The supplied {label} collection contains records that do not belong to execution run '{executionRunId:N}'.");
        }
    }
}

internal sealed record ExecutionSliceSaveResult(
    bool Changed,
    ExecutionStorageIndex Index,
    ExecutionRunDetail Detail);

internal sealed record ExecutionStorageIndex(
    string Version,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    int SessionCount,
    int RunCount,
    int LogCount,
    int MetricCount,
    int ApprovalCount,
    int ArtifactCount,
    int CheckpointCount,
    int ReceiptCount);

internal sealed record ExecutionChatIndex(
    string Version,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ChatSessionSummaryRecord> SessionSummaries,
    IReadOnlyList<ChatRunSummaryRecord> RunSummaries);
