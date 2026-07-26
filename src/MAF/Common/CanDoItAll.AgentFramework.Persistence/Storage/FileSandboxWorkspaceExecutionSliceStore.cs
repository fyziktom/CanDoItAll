using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceExecutionSliceStore(
    FileSandboxWorkspaceStorageLayout layout,
    FileSandboxWorkspaceJsonStore jsonStore)
{
    public bool ExecutionStorageExists() => layout.ExecutionStorageExists();

    public bool HasPersistedIndex() => File.Exists(layout.ExecutionIndexPath);

    public async Task<SandboxWorkspaceExecutionState> LoadAsync(CancellationToken cancellationToken)
    {
        if (ExecutionStorageExists())
        {
            return await LoadExecutionSlicesAsync(cancellationToken);
        }

        return await TryLoadLegacyExecutionStateAsync(cancellationToken)
            ?? SandboxWorkspaceExecutionState.Empty;
    }

    public Task<ExecutionStorageIndex> LoadIndexAsync(CancellationToken cancellationToken)
    {
        return ResolveExecutionIndexAsync(cancellationToken);
    }

    public Task<AgentUsageProjection> LoadUsageProjectionAsync(CancellationToken cancellationToken)
    {
        return LoadOrBuildUsageProjectionAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionRunRecord>> ListRunsAsync(CancellationToken cancellationToken)
    {
        if (!ExecutionStorageExists())
        {
            return (await TryLoadLegacyExecutionStateAsync(cancellationToken))?.ExecutionRuns
                   ?? [];
        }

        var runs = new List<ExecutionRunRecord>();
        if (!Directory.Exists(layout.ExecutionRunsRoot))
        {
            return runs;
        }

        foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(Path.Combine(runDirectory, "run.json"), cancellationToken);
            if (run is not null)
            {
                runs.Add(run);
            }
        }

        return runs;
    }

    public async Task<ExecutionRunRecord?> LoadRunAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        if (ExecutionStorageExists())
        {
            return await jsonStore.ReadJsonAsync<ExecutionRunRecord>(layout.RunPath(executionRunId), cancellationToken);
        }

        return (await TryLoadLegacyExecutionStateAsync(cancellationToken))?.ExecutionRuns
            .FirstOrDefault(item => item.Id == executionRunId);
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
            UsageObservations = (await jsonStore.LoadRecordsFromDirectoryAsync<ProviderUsageObservation>(layout.RunUsageRoot(executionRunId), cancellationToken))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList(),
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
            previousDetail?.UsageObservations ?? [],
            normalizedDetail.UsageObservations,
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
            ReceiptCount: currentIndex.ReceiptCount + normalizedDetail.ToolReceipts.Count - (previousDetail?.ToolReceipts.Count ?? 0),
            ActiveRunCount: currentIndex.ActiveRunCount + CountIndexedActiveRuns(normalizedDetail.Run) - CountIndexedActiveRuns(previousDetail?.Run),
            FailedRunCount: currentIndex.FailedRunCount + CountIndexedFailedRuns(normalizedDetail.Run) - CountIndexedFailedRuns(previousDetail?.Run),
            UsageObservationCount: currentIndex.UsageObservationCount + normalizedDetail.UsageObservations.Count - (previousDetail?.UsageObservations.Count ?? 0));

        if (changed || !File.Exists(layout.ExecutionIndexPath) || jsonStore.RequiresSave(currentIndex, nextIndex))
        {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionIndexPath, nextIndex, cancellationToken);
        }

        await SaveUsageProjectionAsync(previousDetail, normalizedDetail, nextIndex, cancellationToken);

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
        var previousUsageByRun = GroupByRunId(previousState.ProviderUsageObservations, item => item.ExecutionRunId ?? Guid.Empty);
        var usageByRun = GroupByRunId(executionState.ProviderUsageObservations, item => item.ExecutionRunId ?? Guid.Empty);
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
        AddChangedRunIds(changedRunIds, previousUsageByRun, usageByRun);
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
                GetItemsForRun(previousUsageByRun, runId),
                GetItemsForRun(usageByRun, runId),
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
            layout.OrphanUsageRoot,
            GetItemsForRun(previousUsageByRun, Guid.Empty),
            usageByRun.TryGetValue(Guid.Empty, out var orphanUsage) ? orphanUsage : [],
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
            ReceiptCount: executionState.ToolExecutionReceipts.Count,
            ActiveRunCount: executionState.ExecutionRuns.Count(IsIndexedActiveRun),
            FailedRunCount: executionState.ExecutionRuns.Count(IsIndexedFailedRun),
            UsageObservationCount: executionState.ProviderUsageObservations.Count);

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

        var currentUsageProjection = await jsonStore.ReadJsonAsync<AgentUsageProjection>(layout.ExecutionUsageIndexPath, cancellationToken);
        var nextUsageProjection = BuildUsageProjection(executionState, nextIndex);
        if (changed ||
            currentUsageProjection is null ||
            !File.Exists(layout.ExecutionUsageIndexPath) ||
            jsonStore.RequiresSave(currentUsageProjection, nextUsageProjection))
        {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionUsageIndexPath, nextUsageProjection, cancellationToken);
        }

        return changed;
    }

    private async Task<SandboxWorkspaceExecutionState> LoadExecutionSlicesAsync(CancellationToken cancellationToken)
    {
        var sessions = await jsonStore.LoadRecordsFromDirectoryAsync<ChatSessionRecord>(layout.ExecutionSessionsRoot, cancellationToken);
        var runs = new List<ExecutionRunRecord>();
        var executionLog = new List<ExecutionLogEntry>();
        var metrics = new List<AgentRunMetric>();
        var usageObservations = new List<ProviderUsageObservation>();
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
                usageObservations.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ProviderUsageObservation>(Path.Combine(runDirectory, "usage"), cancellationToken));
                approvals.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionApprovalRecord>(Path.Combine(runDirectory, "approvals"), cancellationToken));
                artifacts.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionArtifactRecord>(Path.Combine(runDirectory, "audit", "artifacts"), cancellationToken));
                checkpoints.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionWorkflowCheckpointRecord>(Path.Combine(runDirectory, "workflow-checkpoints", "records"), cancellationToken));
                receipts.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ToolExecutionReceiptRecord>(Path.Combine(runDirectory, "audit", "receipts"), cancellationToken));
            }
        }

        executionLog.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(layout.OrphanLogsRoot, cancellationToken));
        metrics.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(layout.OrphanMetricsRoot, cancellationToken));
        usageObservations.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ProviderUsageObservation>(layout.OrphanUsageRoot, cancellationToken));
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
            ProviderUsageObservations = usageObservations,
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
        IReadOnlyList<ProviderUsageObservation> previousUsageObservations,
        IReadOnlyList<ProviderUsageObservation> usageObservations,
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
            layout.RunUsageRoot(run.Id),
            previousUsageObservations,
            usageObservations,
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
            if (await ExecutionIndexNeedsDashboardCountUpgradeAsync(cancellationToken))
            {
                var upgraded = existing with
                {
                    ActiveRunCount = await CountRunFilesAsync(IsIndexedActiveRun, cancellationToken),
                    FailedRunCount = await CountRunFilesAsync(IsIndexedFailedRun, cancellationToken)
                };

                await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionIndexPath, upgraded, cancellationToken);
                return upgraded;
            }

            return existing;
        }

        var rebuilt = new ExecutionStorageIndex(
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
            ReceiptCount: CountRunScopedJsonFiles(runId => layout.RunReceiptsRoot(runId)) + CountJsonFiles(layout.OrphanReceiptsRoot),
            ActiveRunCount: await CountRunFilesAsync(IsIndexedActiveRun, cancellationToken),
            FailedRunCount: await CountRunFilesAsync(IsIndexedFailedRun, cancellationToken),
            UsageObservationCount: CountRunScopedJsonFiles(runId => layout.RunUsageRoot(runId)) + CountJsonFiles(layout.OrphanUsageRoot));

        await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionIndexPath, rebuilt, cancellationToken);
        return rebuilt;
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

    private async Task<bool> ExecutionIndexNeedsDashboardCountUpgradeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(layout.ExecutionIndexPath))
        {
            return false;
        }

        var rawJson = await jsonStore.ReadTextAsync(layout.ExecutionIndexPath, cancellationToken);
        return !rawJson.Contains("\"activeRunCount\"", StringComparison.Ordinal) ||
               !rawJson.Contains("\"failedRunCount\"", StringComparison.Ordinal);
    }

    private async Task<int> CountRunFilesAsync(Func<ExecutionRunRecord, bool> predicate, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(layout.ExecutionRunsRoot))
        {
            return 0;
        }

        var count = 0;
        foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot))
        {
            var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(Path.Combine(runDirectory, "run.json"), cancellationToken);
            if (run is not null && predicate(run))
            {
                count++;
            }
        }

        return count;
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

    private static int CountIndexedActiveRuns(ExecutionRunRecord? run)
    {
        return run is not null && IsIndexedActiveRun(run) ? 1 : 0;
    }

    private static int CountIndexedFailedRuns(ExecutionRunRecord? run)
    {
        return run is not null && IsIndexedFailedRun(run) ? 1 : 0;
    }

    private static bool IsIndexedActiveRun(ExecutionRunRecord run)
    {
        return run.UpdatedAtUtc >= DateTimeOffset.UtcNow.AddHours(-1) &&
               run.State is ExecutionState.Preparing or ExecutionState.Running or ExecutionState.WaitingOnTool or ExecutionState.Persisting;
    }

    private static bool IsIndexedFailedRun(ExecutionRunRecord run)
    {
        return run.Outcome == RunOutcome.Failed;
    }

    private async Task<AgentUsageProjection> LoadOrBuildUsageProjectionAsync(CancellationToken cancellationToken)
    {
        var executionIndex = await ResolveExecutionIndexAsync(cancellationToken);
        var projection = await jsonStore.ReadJsonAsync<AgentUsageProjection>(layout.ExecutionUsageIndexPath, cancellationToken);
        if (projection is not null &&
            projection.Revision == executionIndex.Revision &&
            string.Equals(projection.Version, executionIndex.Version, StringComparison.Ordinal))
        {
            return projection;
        }

        var rebuilt = BuildUsageProjection(await LoadUsageProjectionSourceAsync(cancellationToken), executionIndex);
        if (projection is null ||
            !File.Exists(layout.ExecutionUsageIndexPath) ||
            jsonStore.RequiresSave(projection, rebuilt))
        {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionUsageIndexPath, rebuilt, cancellationToken);
        }

        return rebuilt;
    }

    private async Task SaveUsageProjectionAsync(
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail normalizedDetail,
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
        var currentProjection = await jsonStore.ReadJsonAsync<AgentUsageProjection>(layout.ExecutionUsageIndexPath, cancellationToken);
        var canApplyDelta = currentProjection is not null &&
                            (currentProjection.Revision == executionIndex.Revision ||
                             currentProjection.Revision == executionIndex.Revision - 1);
        var nextProjection = canApplyDelta
            ? ApplyUsageProjectionDelta(currentProjection!, previousDetail, normalizedDetail, executionIndex)
            : BuildUsageProjection(await LoadUsageProjectionSourceAsync(cancellationToken), executionIndex);

        if (currentProjection is null ||
            !File.Exists(layout.ExecutionUsageIndexPath) ||
            jsonStore.RequiresSave(currentProjection, nextProjection))
        {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionUsageIndexPath, nextProjection, cancellationToken);
        }
    }

    private async Task<SandboxWorkspaceExecutionState> LoadUsageProjectionSourceAsync(CancellationToken cancellationToken)
    {
        if (!ExecutionStorageExists())
        {
            return await LoadAsync(cancellationToken);
        }

        var usageObservations = new List<ProviderUsageObservation>();
        if (Directory.Exists(layout.ExecutionRunsRoot))
        {
            foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                if (!Guid.TryParse(Path.GetFileName(runDirectory), out var runId))
                {
                    continue;
                }

                usageObservations.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ProviderUsageObservation>(
                    layout.RunUsageRoot(runId),
                    cancellationToken));
            }
        }

        usageObservations.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ProviderUsageObservation>(
            layout.OrphanUsageRoot,
            cancellationToken));

        return new SandboxWorkspaceExecutionState(
            Version: "3.0",
            ChatSessions: [],
            ExecutionLog: [],
            Metrics: [])
        {
            ExecutionRuns = await ListRunsAsync(cancellationToken),
            ProviderUsageObservations = usageObservations
        };
    }

    private static AgentUsageProjection ApplyUsageProjectionDelta(
        AgentUsageProjection currentProjection,
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail normalizedDetail,
        ExecutionStorageIndex executionIndex)
    {
        var agentRows = currentProjection.Agents
            .Select(CreateAgentAccumulator)
            .ToDictionary(item => item.AgentId);
        var providerRows = currentProjection.Providers
            .Select(CreateProviderAccumulator)
            .ToDictionary(item => CreateProviderKey(item.ProviderName, item.ProviderKind), StringComparer.OrdinalIgnoreCase);
        var modelRows = currentProjection.Models
            .Select(CreateModelAccumulator)
            .ToDictionary(item => CreateModelKey(item.ProviderName, item.ProviderKind, item.Model), StringComparer.OrdinalIgnoreCase);

        if (previousDetail is not null)
        {
            SubtractRunContribution(agentRows, providerRows, modelRows, previousDetail);
        }

        AddRunContribution(agentRows, providerRows, modelRows, normalizedDetail);

        return new AgentUsageProjection(
            Version: string.IsNullOrWhiteSpace(executionIndex.Version) ? currentProjection.Version : executionIndex.Version,
            Revision: executionIndex.Revision,
            UpdatedAtUtc: executionIndex.UpdatedAtUtc,
            Agents: OrderAgentRows(agentRows.Values),
            Providers: OrderProviderRows(providerRows.Values),
            Models: OrderModelRows(modelRows.Values));
    }

    private static AgentUsageProjection BuildUsageProjection(
        SandboxWorkspaceExecutionState executionState,
        ExecutionStorageIndex executionIndex)
    {
        var agentRows = new Dictionary<Guid, AgentUsageProjectionAccumulator>();
        var providerRows = new Dictionary<string, ProviderUsageProjectionAccumulator>(StringComparer.OrdinalIgnoreCase);
        var modelRows = new Dictionary<string, ModelUsageProjectionAccumulator>(StringComparer.OrdinalIgnoreCase);
        var details = executionState.ExecutionRuns
            .Select(run => new ExecutionRunDetail(
                run,
                ChatSession: null,
                ExecutionLog: [],
                Metrics: []))
            .ToDictionary(detail => detail.Run.Id);

        foreach (var usageGroup in executionState.ProviderUsageObservations
                     .Where(item => item.ExecutionRunId.HasValue)
                     .GroupBy(item => item.ExecutionRunId!.Value))
        {
            if (details.TryGetValue(usageGroup.Key, out var detail))
            {
                details[usageGroup.Key] = detail with
                {
                    UsageObservations = usageGroup.ToList()
                };
            }
        }

        foreach (var detail in details.Values)
        {
            AddRunContribution(agentRows, providerRows, modelRows, detail);
        }

        foreach (var orphanUsage in executionState.ProviderUsageObservations.Where(item => !item.ExecutionRunId.HasValue))
        {
            AddProviderUsage(providerRows, orphanUsage, failedRunDelta: 0);
            AddModelUsage(modelRows, orphanUsage);
        }

        return new AgentUsageProjection(
            Version: string.IsNullOrWhiteSpace(executionIndex.Version) ? executionState.Version : executionIndex.Version,
            Revision: executionIndex.Revision,
            UpdatedAtUtc: executionIndex.UpdatedAtUtc,
            Agents: OrderAgentRows(agentRows.Values),
            Providers: OrderProviderRows(providerRows.Values),
            Models: OrderModelRows(modelRows.Values));
    }

    private static void AddRunContribution(
        IDictionary<Guid, AgentUsageProjectionAccumulator> agentRows,
        IDictionary<string, ProviderUsageProjectionAccumulator> providerRows,
        IDictionary<string, ModelUsageProjectionAccumulator> modelRows,
        ExecutionRunDetail detail)
    {
        AddAgentRun(agentRows, detail, delta: 1);
        foreach (var observation in detail.UsageObservations)
        {
            AddAgentUsage(agentRows, detail.Run.AgentId, observation, delta: 1);
            AddProviderUsage(providerRows, observation, failedRunDelta: 0);
            AddModelUsage(modelRows, observation);
        }

        if (detail.Run.Outcome == RunOutcome.Failed)
        {
            AddProviderFailure(providerRows, detail.Run.ProviderName, failedRunDelta: 1);
        }
    }

    private static void SubtractRunContribution(
        IDictionary<Guid, AgentUsageProjectionAccumulator> agentRows,
        IDictionary<string, ProviderUsageProjectionAccumulator> providerRows,
        IDictionary<string, ModelUsageProjectionAccumulator> modelRows,
        ExecutionRunDetail detail)
    {
        AddAgentRun(agentRows, detail, delta: -1);
        foreach (var observation in detail.UsageObservations)
        {
            AddAgentUsage(agentRows, detail.Run.AgentId, observation, delta: -1);
            SubtractProviderUsage(providerRows, observation);
            SubtractModelUsage(modelRows, observation);
        }

        if (detail.Run.Outcome == RunOutcome.Failed)
        {
            AddProviderFailure(providerRows, detail.Run.ProviderName, failedRunDelta: -1);
        }
    }

    private static void AddAgentRun(
        IDictionary<Guid, AgentUsageProjectionAccumulator> agentRows,
        ExecutionRunDetail detail,
        int delta)
    {
        var row = GetOrAddAgent(agentRows, detail.Run.AgentId);
        row.RunCount = Math.Max(0, row.RunCount + delta);
        row.FailedRunCount = Math.Max(0, row.FailedRunCount + (detail.Run.Outcome == RunOutcome.Failed ? delta : 0));
        row.LastUsedAtUtc = MaxDate(row.LastUsedAtUtc, detail.Run.UpdatedAtUtc);
        RemoveIfEmpty(agentRows, detail.Run.AgentId, row);
    }

    private static void AddAgentUsage(
        IDictionary<Guid, AgentUsageProjectionAccumulator> agentRows,
        Guid agentId,
        ProviderUsageObservation observation,
        int delta)
    {
        var row = GetOrAddAgent(agentRows, agentId);
        ApplyUsage(row, observation, delta);
        row.LastUsedAtUtc = MaxDate(row.LastUsedAtUtc, observation.CreatedAtUtc);
        RemoveIfEmpty(agentRows, agentId, row);
    }

    private static void AddProviderUsage(
        IDictionary<string, ProviderUsageProjectionAccumulator> providerRows,
        ProviderUsageObservation observation,
        int failedRunDelta)
    {
        var key = CreateProviderKey(observation.ProviderName, observation.ProviderKind);
        var row = GetOrAddProvider(providerRows, observation.ProviderName, observation.ProviderKind);
        ApplyUsage(row, observation, delta: 1);
        row.FailedRunCount = Math.Max(0, row.FailedRunCount + failedRunDelta);
        row.LastUsedAtUtc = MaxDate(row.LastUsedAtUtc, observation.CreatedAtUtc);
        providerRows[key] = row;
    }

    private static void SubtractProviderUsage(
        IDictionary<string, ProviderUsageProjectionAccumulator> providerRows,
        ProviderUsageObservation observation)
    {
        var key = CreateProviderKey(observation.ProviderName, observation.ProviderKind);
        if (!providerRows.TryGetValue(key, out var row))
        {
            return;
        }

        ApplyUsage(row, observation, delta: -1);
        RemoveIfEmpty(providerRows, key, row);
    }

    private static void AddProviderFailure(
        IDictionary<string, ProviderUsageProjectionAccumulator> providerRows,
        string providerName,
        int failedRunDelta)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return;
        }

        var match = providerRows.Values.FirstOrDefault(item =>
            string.Equals(item.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        match.FailedRunCount = Math.Max(0, match.FailedRunCount + failedRunDelta);
    }

    private static void AddModelUsage(
        IDictionary<string, ModelUsageProjectionAccumulator> modelRows,
        ProviderUsageObservation observation)
    {
        var key = CreateModelKey(observation.ProviderName, observation.ProviderKind, observation.Model);
        var row = GetOrAddModel(modelRows, observation.ProviderName, observation.ProviderKind, observation.Model);
        ApplyUsage(row, observation, delta: 1);
        row.LastUsedAtUtc = MaxDate(row.LastUsedAtUtc, observation.CreatedAtUtc);
        modelRows[key] = row;
    }

    private static void SubtractModelUsage(
        IDictionary<string, ModelUsageProjectionAccumulator> modelRows,
        ProviderUsageObservation observation)
    {
        var key = CreateModelKey(observation.ProviderName, observation.ProviderKind, observation.Model);
        if (!modelRows.TryGetValue(key, out var row))
        {
            return;
        }

        ApplyUsage(row, observation, delta: -1);
        RemoveIfEmpty(modelRows, key, row);
    }

    private static void ApplyUsage(UsageProjectionAccumulator row, ProviderUsageObservation observation, int delta)
    {
        var known = ProviderPricingCalculator.IsKnownUsageStatus(observation.UsageStatus);
        row.UsageObservationCount = Math.Max(0, row.UsageObservationCount + delta);
        row.KnownUsageObservationCount = Math.Max(0, row.KnownUsageObservationCount + (known ? delta : 0));
        row.UnknownUsageObservationCount = Math.Max(0, row.UnknownUsageObservationCount + (known ? 0 : delta));

        if (!known)
        {
            return;
        }

        row.InputTokens = Math.Max(0, row.InputTokens + observation.InputTokens * delta);
        row.CachedInputTokens = Math.Max(0, row.CachedInputTokens + observation.CachedInputTokens * delta);
        row.OutputTokens = Math.Max(0, row.OutputTokens + observation.OutputTokens * delta);
        row.ReasoningTokens = Math.Max(0, row.ReasoningTokens + observation.ReasoningTokens * delta);
        row.TotalTokens = Math.Max(0, row.TotalTokens + ResolveTotalTokens(observation) * delta);
        row.KnownCostUsd = Math.Max(0m, row.KnownCostUsd + ResolveKnownCost(observation) * delta);
    }

    private static int ResolveTotalTokens(ProviderUsageObservation observation)
    {
        return observation.TotalTokens > 0
            ? observation.TotalTokens
            : Math.Max(0, observation.InputTokens) + Math.Max(0, observation.OutputTokens);
    }

    private static decimal ResolveKnownCost(ProviderUsageObservation observation)
    {
        return observation.ProviderCostUsd ?? observation.CalculatedCostUsd ?? 0m;
    }

    private static DateTimeOffset? MaxDate(DateTimeOffset? current, DateTimeOffset candidate)
    {
        return current.HasValue && current.Value >= candidate
            ? current
            : candidate;
    }

    private static AgentUsageProjectionAccumulator GetOrAddAgent(
        IDictionary<Guid, AgentUsageProjectionAccumulator> rows,
        Guid agentId)
    {
        if (!rows.TryGetValue(agentId, out var row))
        {
            row = new AgentUsageProjectionAccumulator(agentId);
            rows[agentId] = row;
        }

        return row;
    }

    private static AgentUsageProjectionAccumulator CreateAgentAccumulator(AgentUsageProjectionRow row)
    {
        return new AgentUsageProjectionAccumulator(row.AgentId)
        {
            RunCount = row.RunCount,
            FailedRunCount = row.FailedRunCount,
            UsageObservationCount = row.UsageObservationCount,
            KnownUsageObservationCount = row.KnownUsageObservationCount,
            UnknownUsageObservationCount = row.UnknownUsageObservationCount,
            InputTokens = row.InputTokens,
            CachedInputTokens = row.CachedInputTokens,
            OutputTokens = row.OutputTokens,
            ReasoningTokens = row.ReasoningTokens,
            TotalTokens = row.TotalTokens,
            KnownCostUsd = row.KnownCostUsd,
            LastUsedAtUtc = row.LastUsedAtUtc
        };
    }

    private static ProviderUsageProjectionAccumulator GetOrAddProvider(
        IDictionary<string, ProviderUsageProjectionAccumulator> rows,
        string providerName,
        ProviderKind providerKind)
    {
        var key = CreateProviderKey(providerName, providerKind);
        if (!rows.TryGetValue(key, out var row))
        {
            row = new ProviderUsageProjectionAccumulator(
                NormalizeProviderName(providerName),
                providerKind);
            rows[key] = row;
        }

        return row;
    }

    private static ProviderUsageProjectionAccumulator CreateProviderAccumulator(ProviderUsageProjectionRow row)
    {
        return new ProviderUsageProjectionAccumulator(row.ProviderName, row.ProviderKind)
        {
            UsageObservationCount = row.UsageObservationCount,
            KnownUsageObservationCount = row.KnownUsageObservationCount,
            UnknownUsageObservationCount = row.UnknownUsageObservationCount,
            InputTokens = row.InputTokens,
            CachedInputTokens = row.CachedInputTokens,
            OutputTokens = row.OutputTokens,
            ReasoningTokens = row.ReasoningTokens,
            TotalTokens = row.TotalTokens,
            KnownCostUsd = row.KnownCostUsd,
            FailedRunCount = row.FailedRunCount,
            LastUsedAtUtc = row.LastUsedAtUtc
        };
    }

    private static ModelUsageProjectionAccumulator GetOrAddModel(
        IDictionary<string, ModelUsageProjectionAccumulator> rows,
        string providerName,
        ProviderKind providerKind,
        string model)
    {
        var key = CreateModelKey(providerName, providerKind, model);
        if (!rows.TryGetValue(key, out var row))
        {
            row = new ModelUsageProjectionAccumulator(
                NormalizeProviderName(providerName),
                providerKind,
                NormalizeModel(model));
            rows[key] = row;
        }

        return row;
    }

    private static ModelUsageProjectionAccumulator CreateModelAccumulator(ModelUsageProjectionRow row)
    {
        return new ModelUsageProjectionAccumulator(row.ProviderName, row.ProviderKind, row.Model)
        {
            UsageObservationCount = row.UsageObservationCount,
            KnownUsageObservationCount = row.KnownUsageObservationCount,
            UnknownUsageObservationCount = row.UnknownUsageObservationCount,
            InputTokens = row.InputTokens,
            CachedInputTokens = row.CachedInputTokens,
            OutputTokens = row.OutputTokens,
            ReasoningTokens = row.ReasoningTokens,
            TotalTokens = row.TotalTokens,
            KnownCostUsd = row.KnownCostUsd,
            LastUsedAtUtc = row.LastUsedAtUtc
        };
    }

    private static string CreateProviderKey(ProviderUsageProjectionRow row)
    {
        return CreateProviderKey(row.ProviderName, row.ProviderKind);
    }

    private static string CreateProviderKey(string providerName, ProviderKind providerKind)
    {
        return $"{providerKind:D}:{NormalizeProviderName(providerName)}";
    }

    private static string CreateModelKey(ModelUsageProjectionRow row)
    {
        return CreateModelKey(row.ProviderName, row.ProviderKind, row.Model);
    }

    private static string CreateModelKey(string providerName, ProviderKind providerKind, string model)
    {
        return $"{providerKind:D}:{NormalizeProviderName(providerName)}:{NormalizeModel(model)}";
    }

    private static string NormalizeProviderName(string providerName)
    {
        return string.IsNullOrWhiteSpace(providerName) ? "Unknown provider" : providerName.Trim();
    }

    private static string NormalizeModel(string model)
    {
        return string.IsNullOrWhiteSpace(model) ? "Unknown model" : model.Trim();
    }

    private static IReadOnlyList<AgentUsageProjectionRow> OrderAgentRows(IEnumerable<AgentUsageProjectionAccumulator> rows)
    {
        return rows
            .Where(item => !item.IsEmpty)
            .Select(item => item.ToRow())
            .OrderByDescending(item => item.UsageObservationCount)
            .ThenByDescending(item => item.RunCount)
            .ThenByDescending(item => item.TotalTokens)
            .ThenBy(item => item.AgentId)
            .ToList();
    }

    private static IReadOnlyList<ProviderUsageProjectionRow> OrderProviderRows(IEnumerable<ProviderUsageProjectionAccumulator> rows)
    {
        return rows
            .Where(item => !item.IsEmpty)
            .Select(item => item.ToRow())
            .OrderByDescending(item => item.UsageObservationCount)
            .ThenByDescending(item => item.TotalTokens)
            .ThenBy(item => item.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ModelUsageProjectionRow> OrderModelRows(IEnumerable<ModelUsageProjectionAccumulator> rows)
    {
        return rows
            .Where(item => !item.IsEmpty)
            .Select(item => item.ToRow())
            .OrderByDescending(item => item.UsageObservationCount)
            .ThenByDescending(item => item.TotalTokens)
            .ThenBy(item => item.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void RemoveIfEmpty(
        IDictionary<Guid, AgentUsageProjectionAccumulator> rows,
        Guid key,
        AgentUsageProjectionAccumulator row)
    {
        if (row.IsEmpty)
        {
            rows.Remove(key);
        }
    }

    private static void RemoveIfEmpty(
        IDictionary<string, ProviderUsageProjectionAccumulator> rows,
        string key,
        ProviderUsageProjectionAccumulator row)
    {
        if (row.IsEmpty)
        {
            rows.Remove(key);
        }
    }

    private static void RemoveIfEmpty(
        IDictionary<string, ModelUsageProjectionAccumulator> rows,
        string key,
        ModelUsageProjectionAccumulator row)
    {
        if (row.IsEmpty)
        {
            rows.Remove(key);
        }
    }

    private static ExecutionRunDetail NormalizeRunDetail(ExecutionRunDetail detail)
    {
        return new ExecutionRunDetail(
            Run: detail.Run,
            ChatSession: detail.ChatSession,
            ExecutionLog: detail.ExecutionLog.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Metrics: detail.Metrics.OrderByDescending(item => item.CreatedAtUtc).ToList())
        {
            UsageObservations = detail.UsageObservations.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Approvals = detail.Approvals.OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc).ToList(),
            Artifacts = detail.Artifacts.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Checkpoints = detail.Checkpoints.OrderByDescending(item => item.CapturedAtUtc).ToList(),
            ToolReceipts = detail.ToolReceipts.Select(NormalizeToolReceipt).OrderByDescending(item => item.CompletedAtUtc).ToList()
        };
    }

    private static ToolExecutionReceiptRecord NormalizeToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        return receipt with
        {
            RequestSummary = NormalizeReceiptText(receipt.RequestSummary),
            WorkingDirectory = NormalizeReceiptText(receipt.WorkingDirectory),
            ExitSummary = NormalizeReceiptText(receipt.ExitSummary)
        };
    }

    private static string NormalizeReceiptText(string value)
    {
        var redacted = WorkflowExecutorRedaction.RedactText(value);
        return WorkflowPayloadPolicyService.BoundPayload(redacted, WorkflowEventPayloads.MaxInlinePayloadCharacters);
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
        EnsureRunScoped(detail.Run.Id, detail.UsageObservations.Select(item => item.ExecutionRunId ?? Guid.Empty), "provider usage observation");
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
    int ReceiptCount,
    int ActiveRunCount,
    int FailedRunCount,
    int UsageObservationCount = 0);

internal sealed record ExecutionChatIndex(
    string Version,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ChatSessionSummaryRecord> SessionSummaries,
    IReadOnlyList<ChatRunSummaryRecord> RunSummaries);

internal abstract class UsageProjectionAccumulator
{
    public int UsageObservationCount { get; set; }

    public int KnownUsageObservationCount { get; set; }

    public int UnknownUsageObservationCount { get; set; }

    public int InputTokens { get; set; }

    public int CachedInputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal KnownCostUsd { get; set; }

    public DateTimeOffset? LastUsedAtUtc { get; set; }

    public virtual bool IsEmpty => UsageObservationCount == 0;
}

internal sealed class AgentUsageProjectionAccumulator(Guid agentId) : UsageProjectionAccumulator
{
    public Guid AgentId { get; } = agentId;

    public int RunCount { get; set; }

    public int FailedRunCount { get; set; }

    public override bool IsEmpty => RunCount == 0 && UsageObservationCount == 0;

    public AgentUsageProjectionRow ToRow()
    {
        return new AgentUsageProjectionRow(
            AgentId,
            RunCount,
            FailedRunCount,
            UsageObservationCount,
            KnownUsageObservationCount,
            UnknownUsageObservationCount,
            InputTokens,
            CachedInputTokens,
            OutputTokens,
            ReasoningTokens,
            TotalTokens,
            decimal.Round(KnownCostUsd, 6, MidpointRounding.AwayFromZero),
            LastUsedAtUtc);
    }
}

internal sealed class ProviderUsageProjectionAccumulator(
    string providerName,
    ProviderKind providerKind) : UsageProjectionAccumulator
{
    public string ProviderName { get; } = providerName;

    public ProviderKind ProviderKind { get; } = providerKind;

    public int FailedRunCount { get; set; }

    public ProviderUsageProjectionRow ToRow()
    {
        return new ProviderUsageProjectionRow(
            ProviderName,
            ProviderKind,
            UsageObservationCount,
            KnownUsageObservationCount,
            UnknownUsageObservationCount,
            InputTokens,
            CachedInputTokens,
            OutputTokens,
            ReasoningTokens,
            TotalTokens,
            decimal.Round(KnownCostUsd, 6, MidpointRounding.AwayFromZero),
            FailedRunCount,
            LastUsedAtUtc);
    }
}

internal sealed class ModelUsageProjectionAccumulator(
    string providerName,
    ProviderKind providerKind,
    string model) : UsageProjectionAccumulator
{
    public string ProviderName { get; } = providerName;

    public ProviderKind ProviderKind { get; } = providerKind;

    public string Model { get; } = model;

    public ModelUsageProjectionRow ToRow()
    {
        return new ModelUsageProjectionRow(
            ProviderName,
            ProviderKind,
            Model,
            UsageObservationCount,
            KnownUsageObservationCount,
            UnknownUsageObservationCount,
            InputTokens,
            CachedInputTokens,
            OutputTokens,
            ReasoningTokens,
            TotalTokens,
            decimal.Round(KnownCostUsd, 6, MidpointRounding.AwayFromZero),
            LastUsedAtUtc);
    }
}
