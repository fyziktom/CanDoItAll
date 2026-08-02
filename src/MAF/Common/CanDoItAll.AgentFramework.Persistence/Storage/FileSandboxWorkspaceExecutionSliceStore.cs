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

    public Task<ExecutionStorageIndex> LoadIndexAsync(CancellationToken cancellationToken)
    {
        return ResolveExecutionIndexAsync(cancellationToken);
    }

    public Task<AgentUsageProjection> LoadUsageProjectionAsync(CancellationToken cancellationToken)
    {
        return LoadOrBuildUsageProjectionAsync(cancellationToken);
    }

    public async Task<AgentExecutionDeletionPlan> PrepareAgentDeletionAsync(
        Guid agentId,
        ExecutionStorageIndex currentIndex,
        ExecutionChatIndex currentChatIndex,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent identifier is required.", nameof(agentId));
        }

        ArgumentNullException.ThrowIfNull(currentIndex);
        ArgumentNullException.ThrowIfNull(currentChatIndex);
        if (currentChatIndex.Revision != currentIndex.Revision)
        {
            throw new InvalidDataException(
                "The execution chat index revision does not match the canonical execution index before agent deletion.");
        }

        var sessionIds = currentChatIndex.SessionSummaries
            .Where(item => item.AgentId == agentId)
            .Select(item => item.Id)
            .ToHashSet();
        var runSummaries = currentChatIndex.RunSummaries
            .Where(item =>
                item.AgentId == agentId ||
                item.ChatSessionId.HasValue &&
                sessionIds.Contains(item.ChatSessionId.Value))
            .ToList();
        var activeRun = runSummaries.FirstOrDefault(item => IsIndexedActiveState(item.State));
        if (activeRun is not null)
        {
            throw new AgentDeletionConflictException(
                agentId,
                AgentDeletionConflictKind.ActiveExecution,
                $"Agent '{agentId:D}' cannot be deleted while execution run '{activeRun.ExecutionRunId:D}' is active.");
        }

        var runIds = runSummaries
            .Select(item => item.ExecutionRunId)
            .ToHashSet();
        var runSummariesById = runSummaries.ToDictionary(
            item => item.ExecutionRunId);
        var runDetails = new List<ExecutionRunDetail>(runIds.Count);
        foreach (var runId in runIds.OrderBy(item => item))
        {
            var detail = await LoadRunDetailAsync(runId, cancellationToken)
                ?? throw new InvalidDataException(
                    $"Execution run '{runId:N}' disappeared while preparing agent deletion.");
            var summary = runSummariesById[runId];
            if (detail.Run.AgentId != summary.AgentId ||
                detail.Run.ChatSessionId != summary.ChatSessionId)
            {
                throw new InvalidDataException(
                    $"Execution run '{runId:N}' does not match its canonical chat index summary.");
            }

            if (IsIndexedActiveState(detail.Run.State))
            {
                throw new AgentDeletionConflictException(
                    agentId,
                    AgentDeletionConflictKind.ActiveExecution,
                    $"Agent '{agentId:D}' cannot be deleted while execution run '{runId:D}' is active.");
            }

            runDetails.Add(detail);
        }

        var orphanLogs = (await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(
                layout.OrphanLogsRoot,
                cancellationToken))
            .Where(item => MatchesDeletedAgent(item.AgentId, item.ChatSessionId, item.ExecutionRunId, agentId, sessionIds, runIds))
            .ToList();
        var orphanMetrics = (await jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(
                layout.OrphanMetricsRoot,
                cancellationToken))
            .Where(item => MatchesDeletedAgent(item.AgentId, item.ChatSessionId, item.ExecutionRunId, agentId, sessionIds, runIds))
            .ToList();
        var allOrphanUsage = await jsonStore.LoadRecordsFromDirectoryAsync<ProviderUsageObservation>(
            layout.OrphanUsageRoot,
            cancellationToken);
        var orphanUsage = allOrphanUsage
            .Where(item =>
                item.AgentId == agentId ||
                item.ChatSessionId.HasValue && sessionIds.Contains(item.ChatSessionId.Value) ||
                item.ExecutionRunId.HasValue && runIds.Contains(item.ExecutionRunId.Value))
            .ToList();
        var orphanApprovals = (await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionApprovalRecord>(
                layout.OrphanApprovalsRoot,
                cancellationToken))
            .Where(item => runIds.Contains(item.ExecutionRunId))
            .ToList();
        var orphanArtifacts = (await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionArtifactRecord>(
                layout.OrphanArtifactsRoot,
                cancellationToken))
            .Where(item => runIds.Contains(item.ExecutionRunId))
            .ToList();
        var orphanReceipts = (await jsonStore.LoadRecordsFromDirectoryAsync<ToolExecutionReceiptRecord>(
                layout.OrphanReceiptsRoot,
                cancellationToken))
            .Where(item => runIds.Contains(item.ExecutionRunId))
            .ToList();

        var hasExecutionChanges =
            runDetails.Count > 0 ||
            sessionIds.Count > 0 ||
            orphanLogs.Count > 0 ||
            orphanMetrics.Count > 0 ||
            orphanUsage.Count > 0 ||
            orphanApprovals.Count > 0 ||
            orphanArtifacts.Count > 0 ||
            orphanReceipts.Count > 0;
        if (!hasExecutionChanges)
        {
            return new AgentExecutionDeletionPlan(
                agentId,
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                currentIndex,
                currentIndex,
                TargetUsageProjection: null,
                SourceUsageProjection: null,
                HasExecutionChanges: false);
        }

        var targetIndex = CreateAgentDeletionTargetIndex(
            currentIndex,
            runDetails,
            sessionIds.Count,
            orphanLogs.Count,
            orphanMetrics.Count,
            orphanUsage.Count,
            orphanApprovals.Count,
            orphanArtifacts.Count,
            orphanReceipts.Count,
            deletedAtUtc);
        var currentUsageProjection = await LoadOrBuildUsageProjectionAsync(
            currentIndex,
            cancellationToken);
        var targetUsageProjection = await CreateAgentDeletionUsageProjectionAsync(
            agentId,
            currentUsageProjection,
            runDetails,
            orphanUsage,
            targetIndex,
            cancellationToken);

        return new AgentExecutionDeletionPlan(
            agentId,
            runDetails.Select(item => item.Run.Id).ToList(),
            sessionIds.OrderBy(item => item).ToList(),
            orphanLogs,
            orphanMetrics,
            orphanUsage,
            orphanApprovals,
            orphanArtifacts,
            orphanReceipts,
            currentIndex,
            targetIndex,
            targetUsageProjection,
            currentUsageProjection,
            HasExecutionChanges: true);
    }

    public async Task PersistAgentDeletionAsync(
        AgentExecutionDeletionPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.HasExecutionChanges)
        {
            return;
        }

        foreach (var runId in plan.RunIds)
        {
            DeleteRunRoot(runId);
        }

        foreach (var sessionId in plan.SessionIds)
        {
            DeleteFileIfExists(layout.SessionPath(sessionId));
        }

        foreach (var item in plan.OrphanLogs)
        {
            DeleteFileIfExists(Path.Combine(layout.OrphanLogsRoot, $"{item.Id:N}.json"));
        }

        foreach (var item in plan.OrphanMetrics)
        {
            DeleteFileIfExists(Path.Combine(layout.OrphanMetricsRoot, $"{item.Id:N}.json"));
        }

        foreach (var item in plan.OrphanUsage)
        {
            DeleteFileIfExists(Path.Combine(layout.OrphanUsageRoot, $"{item.Id:N}.json"));
        }

        foreach (var item in plan.OrphanApprovals)
        {
            DeleteFileIfExists(Path.Combine(
                layout.OrphanApprovalsRoot,
                $"{item.ExecutionRunId:N}-{jsonStore.NormalizeFileName(item.ApprovalId)}.json"));
        }

        foreach (var item in plan.OrphanArtifacts)
        {
            DeleteFileIfExists(Path.Combine(layout.OrphanArtifactsRoot, $"{item.Id:N}.json"));
        }

        foreach (var item in plan.OrphanReceipts)
        {
            DeleteFileIfExists(Path.Combine(layout.OrphanReceiptsRoot, $"{item.Id:N}.json"));
        }

        await jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionIndexPath,
            plan.TargetIndex,
            cancellationToken);
        await jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionUsageIndexPath,
            plan.TargetUsageProjection
                ?? throw new InvalidDataException(
                    "Agent deletion with execution changes requires a target usage projection."),
            cancellationToken);
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

    public async Task<NewExecutionRunPersistencePlan> PrepareNewRunAsync(
        ExecutionRunDetail detail,
        ChatSessionRecord? previousSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var normalizedDetail = NormalizeRunDetail(detail);
        EnsureRunDetailConsistency(normalizedDetail);
        if (normalizedDetail.ChatSession is null)
        {
            throw new InvalidOperationException(
                $"Execution run '{normalizedDetail.Run.Id:N}' requires a chat session for atomic chat-run persistence.");
        }

        if (previousSession is not null &&
            (previousSession.Id != normalizedDetail.ChatSession.Id ||
             previousSession.AgentId != normalizedDetail.ChatSession.AgentId))
        {
            throw new InvalidOperationException(
                $"Execution run '{normalizedDetail.Run.Id:N}' was prepared with a different chat session.");
        }

        var previousIndex = await ResolveExecutionIndexAsync(cancellationToken);
        var targetIndex = CreateNewRunTargetIndex(
            previousIndex,
            normalizedDetail,
            sessionExistedBefore: previousSession is not null,
            DateTimeOffset.UtcNow);
        var previousUsageProjection =
            await LoadOrBuildUsageProjectionAsync(
                previousIndex,
                cancellationToken);
        if (previousUsageProjection.Revision != previousIndex.Revision ||
            !string.Equals(
                previousUsageProjection.Version,
                previousIndex.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Execution run '{normalizedDetail.Run.Id:N}' cannot be created because its usage projection revision does not match the execution index.");
        }

        return new NewExecutionRunPersistencePlan(
            previousSession,
            normalizedDetail,
            previousIndex,
            targetIndex);
    }

    public async Task<GenericNewExecutionRunPersistencePlan> PrepareGenericNewRunAsync(
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var normalizedDetail = NormalizeRunDetail(detail);
        EnsureRunDetailConsistency(normalizedDetail);
        if (File.Exists(layout.RunPath(normalizedDetail.Run.Id)))
        {
            throw new InvalidOperationException(
                $"Execution run '{normalizedDetail.Run.Id:N}' already exists.");
        }

        var previousSession = normalizedDetail.ChatSession is null
            ? null
            : await jsonStore.ReadJsonAsync<ChatSessionRecord>(
                layout.SessionPath(normalizedDetail.ChatSession.Id),
                cancellationToken);
        if (previousSession is not null &&
            (previousSession.Id != normalizedDetail.ChatSession!.Id ||
             previousSession.AgentId != normalizedDetail.ChatSession.AgentId))
        {
            throw new InvalidOperationException(
                $"Execution run '{normalizedDetail.Run.Id:N}' references an incompatible persisted chat session.");
        }

        var previousIndex = await ResolveExecutionIndexAsync(
            cancellationToken);
        var targetIndex = CreateNewRunTargetIndex(
            previousIndex,
            normalizedDetail,
            sessionExistedBefore: previousSession is not null,
            DateTimeOffset.UtcNow);
        var previousUsageProjection =
            await LoadOrBuildUsageProjectionAsync(
                previousIndex,
                cancellationToken);
        if (previousUsageProjection.Revision != previousIndex.Revision ||
            !string.Equals(
                previousUsageProjection.Version,
                previousIndex.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Execution run '{normalizedDetail.Run.Id:N}' cannot be created because its usage projection revision does not match the execution index.");
        }

        var targetUsageProjection = ApplyUsageProjectionDelta(
            previousUsageProjection,
            previousDetail: null,
            normalizedDetail,
            targetIndex);
        return new GenericNewExecutionRunPersistencePlan(
            previousSession,
            normalizedDetail,
            previousIndex,
            targetIndex,
            previousUsageProjection,
            targetUsageProjection);
    }

    public async Task<ExecutionRunDetail> PersistGenericNewRunSlicesAsync(
        GenericNewExecutionRunPersistencePlan plan,
        bool validatePersistedState,
        CancellationToken cancellationToken)
    {
        ValidateGenericNewRunPlan(plan);

        var detail = plan.Detail;
        var targetSession = detail.ChatSession;
        var storedSession = targetSession is not null && validatePersistedState
            ? await jsonStore.ReadJsonAsync<ChatSessionRecord>(
                layout.SessionPath(targetSession.Id),
                cancellationToken)
            : plan.PreviousSession;
        if (targetSession is not null &&
            storedSession is not null &&
            !HasSamePayload(storedSession, targetSession) &&
            (plan.PreviousSession is null ||
             !HasSamePayload(storedSession, plan.PreviousSession)))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' found an unexpected session payload for '{targetSession.Id:N}'.");
        }

        var storedDetail = validatePersistedState
            ? await LoadRunDetailAsync(
                detail.Run.Id,
                cancellationToken)
            : null;
        if (storedDetail is not null)
        {
            EnsurePersistedGenericNewRunDetailIsCompatible(
                storedDetail,
                detail);
        }

        if (targetSession is not null)
        {
            await jsonStore.WriteJsonIfChangedAsync(
                layout.SessionPath(targetSession.Id),
                targetSession,
                cancellationToken);
        }

        await PersistRunAsync(
            storedDetail?.Run,
            detail.Run,
            storedDetail?.ExecutionLog ?? [],
            detail.ExecutionLog,
            storedDetail?.Metrics ?? [],
            detail.Metrics,
            storedDetail?.UsageObservations ?? [],
            detail.UsageObservations,
            storedDetail?.Approvals ?? [],
            detail.Approvals,
            storedDetail?.Artifacts ?? [],
            detail.Artifacts,
            storedDetail?.Checkpoints ?? [],
            detail.Checkpoints,
            storedDetail?.ToolReceipts ?? [],
            detail.ToolReceipts,
            cancellationToken);

        return detail;
    }

    public async Task ValidateGenericNewRunPersistedStateAsync(
        GenericNewExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        ValidateGenericNewRunPlan(plan);

        var detail = plan.Detail;
        if (detail.ChatSession is not null)
        {
            var storedSession =
                await jsonStore.ReadJsonAsync<ChatSessionRecord>(
                    layout.SessionPath(detail.ChatSession.Id),
                    cancellationToken);
            if (storedSession is not null &&
                !HasSamePayload(storedSession, detail.ChatSession) &&
                (plan.PreviousSession is null ||
                 !HasSamePayload(storedSession, plan.PreviousSession)))
            {
                throw new InvalidDataException(
                    $"Pending generic execution-run creation '{detail.Run.Id:N}' found an unexpected session payload for '{detail.ChatSession.Id:N}'.");
            }
        }

        var storedDetail = await LoadRunDetailAsync(
            detail.Run.Id,
            cancellationToken);
        if (storedDetail is not null)
        {
            EnsurePersistedGenericNewRunDetailIsCompatible(
                storedDetail,
                detail);
        }

        var currentIndex =
            await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
                layout.ExecutionIndexPath,
                cancellationToken);
        if (currentIndex is null ||
            !HasSamePayload(currentIndex, plan.PreviousIndex) &&
            !HasSamePayload(currentIndex, plan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' found an unexpected execution index.");
        }

        var currentUsageProjection =
            await jsonStore.ReadJsonAsync<AgentUsageProjection>(
                layout.ExecutionUsageIndexPath,
                cancellationToken);
        if (currentUsageProjection is null ||
            !HasSamePayload(
                currentUsageProjection,
                plan.PreviousUsageProjection) &&
            !HasSamePayload(
                currentUsageProjection,
                plan.TargetUsageProjection))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' found an unexpected usage projection.");
        }
    }

    public async Task PersistGenericNewRunExecutionIndexAsync(
        GenericNewExecutionRunPersistencePlan plan,
        bool validatePersistedState,
        CancellationToken cancellationToken)
    {
        ValidateGenericNewRunPlan(plan);
        if (validatePersistedState)
        {
            var currentIndex =
                await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
                    layout.ExecutionIndexPath,
                    cancellationToken);
            if (currentIndex is null ||
                !HasSamePayload(currentIndex, plan.PreviousIndex) &&
                !HasSamePayload(currentIndex, plan.TargetIndex))
            {
                throw new InvalidDataException(
                    $"Pending generic execution-run creation '{plan.Detail.Run.Id:N}' found an unexpected execution index.");
            }
        }

        if (validatePersistedState)
        {
            await jsonStore.WriteJsonIfChangedAsync(
                layout.ExecutionIndexPath,
                plan.TargetIndex,
                cancellationToken);
            return;
        }

        await jsonStore.WriteJsonAtomicallyAsync(
            layout.ExecutionIndexPath,
            plan.TargetIndex,
            cancellationToken);
    }

    public async Task PersistGenericNewRunUsageIndexAsync(
        GenericNewExecutionRunPersistencePlan plan,
        bool validatePersistedState,
        CancellationToken cancellationToken)
    {
        ValidateGenericNewRunPlan(plan);
        if (validatePersistedState)
        {
            var currentUsageProjection =
                await jsonStore.ReadJsonAsync<AgentUsageProjection>(
                    layout.ExecutionUsageIndexPath,
                    cancellationToken);
            if (currentUsageProjection is null ||
                !HasSamePayload(
                    currentUsageProjection,
                    plan.PreviousUsageProjection) &&
                !HasSamePayload(
                    currentUsageProjection,
                    plan.TargetUsageProjection))
            {
                throw new InvalidDataException(
                    $"Pending generic execution-run creation '{plan.Detail.Run.Id:N}' found an unexpected usage projection.");
            }
        }

        if (validatePersistedState)
        {
            await jsonStore.WriteJsonIfChangedAsync(
                layout.ExecutionUsageIndexPath,
                plan.TargetUsageProjection,
                cancellationToken);
            return;
        }

        await jsonStore.WriteJsonAtomicallyAsync(
            layout.ExecutionUsageIndexPath,
            plan.TargetUsageProjection,
            cancellationToken);
    }

    public async Task<ExecutionRunDetail> PersistNewRunSlicesAsync(
        NewExecutionRunPersistencePlan plan,
        bool validatePersistedState,
        CancellationToken cancellationToken)
    {
        ValidateNewRunPlan(plan);

        var detail = plan.Detail;
        var targetSession = detail.ChatSession!;
        var storedSession = validatePersistedState
            ? await jsonStore.ReadJsonAsync<ChatSessionRecord>(
                layout.SessionPath(targetSession.Id),
                cancellationToken)
            : plan.PreviousSession;
        if (storedSession is not null &&
            !HasSamePayload(storedSession, targetSession) &&
            (plan.PreviousSession is null ||
             !HasSamePayload(storedSession, plan.PreviousSession)))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{detail.Run.Id:N}' found an unexpected session payload for '{targetSession.Id:N}'.");
        }

        var storedDetail = validatePersistedState
            ? await LoadRunDetailAsync(
                detail.Run.Id,
                cancellationToken)
            : null;
        if (storedDetail is not null)
        {
            EnsurePersistedDetailIsCompatible(storedDetail, detail);
        }

        await jsonStore.WriteJsonIfChangedAsync(
            layout.SessionPath(targetSession.Id),
            targetSession,
            cancellationToken);
        await PersistRunAsync(
            storedDetail?.Run,
            detail.Run,
            storedDetail?.ExecutionLog ?? [],
            detail.ExecutionLog,
            storedDetail?.Metrics ?? [],
            detail.Metrics,
            storedDetail?.UsageObservations ?? [],
            detail.UsageObservations,
            storedDetail?.Approvals ?? [],
            detail.Approvals,
            storedDetail?.Artifacts ?? [],
            detail.Artifacts,
            storedDetail?.Checkpoints ?? [],
            detail.Checkpoints,
            storedDetail?.ToolReceipts ?? [],
            detail.ToolReceipts,
            cancellationToken);

        return detail;
    }

    public async Task PersistNewRunIndexesAsync(
        NewExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        ValidateNewRunPlan(plan);

        var currentIndex = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
            layout.ExecutionIndexPath,
            cancellationToken);
        if (currentIndex is null ||
            !HasSamePayload(currentIndex, plan.PreviousIndex) &&
            !HasSamePayload(currentIndex, plan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{plan.Detail.Run.Id:N}' found an unexpected execution index.");
        }

        await jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionIndexPath,
            plan.TargetIndex,
            cancellationToken);

        var currentUsageProjection =
            await jsonStore.ReadJsonAsync<AgentUsageProjection>(
                layout.ExecutionUsageIndexPath,
                cancellationToken);
        AgentUsageProjection targetUsageProjection;
        if (currentUsageProjection is not null &&
            currentUsageProjection.Revision == plan.PreviousIndex.Revision &&
            string.Equals(
                currentUsageProjection.Version,
                plan.PreviousIndex.Version,
                StringComparison.Ordinal))
        {
            targetUsageProjection = ApplyUsageProjectionDelta(
                currentUsageProjection,
                previousDetail: null,
                plan.Detail,
                plan.TargetIndex);
        }
        else if (currentUsageProjection is not null &&
                 currentUsageProjection.Revision ==
                    plan.TargetIndex.Revision &&
                 string.Equals(
                     currentUsageProjection.Version,
                     plan.TargetIndex.Version,
                     StringComparison.Ordinal))
        {
            targetUsageProjection = currentUsageProjection;
        }
        else
        {
            targetUsageProjection = BuildUsageProjection(
                await LoadUsageProjectionSourceAsync(cancellationToken),
                plan.TargetIndex);
        }

        await jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionUsageIndexPath,
            targetUsageProjection,
            cancellationToken);
    }

    public void ValidateNewRunPlan(NewExecutionRunPersistencePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.Detail);
        ArgumentNullException.ThrowIfNull(plan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(plan.TargetIndex);

        var detail = NormalizeRunDetail(plan.Detail);
        EnsureRunDetailConsistency(detail);
        if (detail.Run.Id == Guid.Empty)
        {
            throw new InvalidDataException(
                "A pending chat-run transaction must identify its execution run.");
        }

        if (detail.ChatSession is null ||
            detail.Run.ChatSessionId != detail.ChatSession.Id)
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{detail.Run.Id:N}' does not contain one matching chat session.");
        }

        if (plan.PreviousSession is not null &&
            (plan.PreviousSession.Id != detail.ChatSession.Id ||
             plan.PreviousSession.AgentId != detail.ChatSession.AgentId))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{detail.Run.Id:N}' contains an invalid previous session.");
        }

        if (!HasSamePayload(detail, plan.Detail))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{detail.Run.Id:N}' contains a non-normalized execution detail.");
        }

        var expectedTargetIndex = CreateNewRunTargetIndex(
            plan.PreviousIndex,
            detail,
            sessionExistedBefore: plan.PreviousSession is not null,
            plan.TargetIndex.UpdatedAtUtc);
        if (!HasSamePayload(expectedTargetIndex, plan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{detail.Run.Id:N}' contains an invalid target execution index.");
        }
    }

    public void ValidateGenericNewRunPlan(
        GenericNewExecutionRunPersistencePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.Detail);
        ArgumentNullException.ThrowIfNull(plan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(plan.TargetIndex);
        ArgumentNullException.ThrowIfNull(plan.PreviousUsageProjection);
        ArgumentNullException.ThrowIfNull(plan.TargetUsageProjection);

        var detail = NormalizeRunDetail(plan.Detail);
        EnsureRunDetailConsistency(detail);
        if (detail.Run.Id == Guid.Empty)
        {
            throw new InvalidDataException(
                "A pending generic execution-run creation must identify its execution run.");
        }

        if (!HasSamePayload(detail, plan.Detail))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' contains a non-normalized execution detail.");
        }

        if (plan.PreviousSession is not null &&
            (detail.ChatSession is null ||
             plan.PreviousSession.Id != detail.ChatSession.Id ||
             plan.PreviousSession.AgentId != detail.ChatSession.AgentId))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' contains an invalid previous session.");
        }

        var expectedTargetIndex = CreateNewRunTargetIndex(
            plan.PreviousIndex,
            detail,
            sessionExistedBefore: plan.PreviousSession is not null,
            plan.TargetIndex.UpdatedAtUtc);
        if (!HasSamePayload(expectedTargetIndex, plan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' contains an invalid target execution index.");
        }

        if (plan.PreviousUsageProjection.Revision !=
                plan.PreviousIndex.Revision ||
            !string.Equals(
                plan.PreviousUsageProjection.Version,
                plan.PreviousIndex.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' contains an invalid previous usage projection.");
        }

        var expectedTargetUsageProjection = ApplyUsageProjectionDelta(
            plan.PreviousUsageProjection,
            previousDetail: null,
            detail,
            plan.TargetIndex);
        if (!HasSamePayload(
                expectedTargetUsageProjection,
                plan.TargetUsageProjection))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{detail.Run.Id:N}' contains an invalid target usage projection.");
        }
    }

    public bool HasSameRunDetailPayload(
        ExecutionRunDetail previousDetail,
        ExecutionRunDetail targetDetail)
    {
        ArgumentNullException.ThrowIfNull(previousDetail);
        ArgumentNullException.ThrowIfNull(targetDetail);

        return HasSamePayload(
            NormalizeRunDetail(previousDetail),
            NormalizeRunDetail(targetDetail));
    }

    public async Task<ExistingExecutionRunPersistencePlan> PrepareExistingRunUpdateAsync(
        ExecutionRunDetail previousDetail,
        ExecutionRunDetail targetDetail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previousDetail);
        ArgumentNullException.ThrowIfNull(targetDetail);

        var normalizedPrevious = NormalizeRunDetail(previousDetail);
        var normalizedTarget = NormalizeRunDetail(targetDetail);
        EnsureRunDetailConsistency(normalizedPrevious);
        EnsureRunDetailConsistency(normalizedTarget);
        EnsureExistingRunIdentityIsStable(
            normalizedPrevious,
            normalizedTarget);
        if (HasSamePayload(normalizedPrevious, normalizedTarget))
        {
            throw new InvalidOperationException(
                $"Execution run '{normalizedPrevious.Run.Id:N}' update does not change its persisted payload.");
        }

        var previousIndex = await ResolveExecutionIndexAsync(
            cancellationToken);
        var targetIndex = CreateExistingRunTargetIndex(
            previousIndex,
            normalizedPrevious,
            normalizedTarget,
            DateTimeOffset.UtcNow);
        var previousUsageProjection = await LoadOrBuildUsageProjectionAsync(
            previousIndex,
            cancellationToken);
        if (previousUsageProjection.Revision != previousIndex.Revision)
        {
            throw new InvalidDataException(
                $"Execution run '{normalizedPrevious.Run.Id:N}' cannot be updated because its usage projection revision does not match the execution index.");
        }

        var targetUsageProjection = ApplyUsageProjectionDelta(
            previousUsageProjection,
            normalizedPrevious,
            normalizedTarget,
            targetIndex);

        return new ExistingExecutionRunPersistencePlan(
            normalizedPrevious,
            normalizedTarget,
            previousIndex,
            targetIndex,
            previousUsageProjection,
            targetUsageProjection);
    }

    public async Task PersistExistingRunSessionAsync(
        ExistingExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.TargetDetail.ChatSession is null)
        {
            return;
        }

        var storedSession = await jsonStore.ReadJsonAsync<ChatSessionRecord>(
            layout.SessionPath(plan.TargetDetail.ChatSession.Id),
            cancellationToken);
        EnsureStoredPayloadIsTransitionCompatible(
            storedSession,
            plan.PreviousDetail.ChatSession,
            plan.TargetDetail.ChatSession,
            plan.TargetDetail.Run.Id,
            "chat session");
        await jsonStore.WriteJsonIfChangedAsync(
            layout.SessionPath(plan.TargetDetail.ChatSession.Id),
            plan.TargetDetail.ChatSession,
            cancellationToken);
    }

    public async Task PersistExistingRunRecordAsync(
        ExistingExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        var storedRun = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
            layout.RunPath(plan.TargetDetail.Run.Id),
            cancellationToken);
        EnsureStoredPayloadIsTransitionCompatible(
            storedRun,
            plan.PreviousDetail.Run,
            plan.TargetDetail.Run,
            plan.TargetDetail.Run.Id,
            "execution run");
        await jsonStore.WriteJsonIfChangedAsync(
            layout.RunPath(plan.TargetDetail.Run.Id),
            plan.TargetDetail.Run,
            cancellationToken);
    }

    public async Task PersistExistingRunApprovalRecordsAsync(
        ExistingExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        await PersistExistingRunRecordCollectionAsync(
            layout.RunApprovalsRoot(plan.TargetDetail.Run.Id),
            plan.PreviousDetail.Approvals,
            plan.TargetDetail.Approvals,
            item => item.ApprovalId,
            item => $"{jsonStore.NormalizeFileName(item.ApprovalId)}.json",
            plan.TargetDetail.Run.Id,
            "execution approval",
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);
    }

    public async Task PersistExistingRunRemainingRecordsAsync(
        ExistingExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        var runId = plan.TargetDetail.Run.Id;
        await PersistExistingRunRecordCollectionAsync(
            layout.RunLogsRoot(runId),
            plan.PreviousDetail.ExecutionLog,
            plan.TargetDetail.ExecutionLog,
            item => item.Id,
            item => $"{item.Id:N}.json",
            runId,
            "execution log",
            EqualityComparer<Guid>.Default,
            cancellationToken);
        await PersistExistingRunRecordCollectionAsync(
            layout.RunMetricsRoot(runId),
            plan.PreviousDetail.Metrics,
            plan.TargetDetail.Metrics,
            item => item.Id,
            item => $"{item.Id:N}.json",
            runId,
            "execution metric",
            EqualityComparer<Guid>.Default,
            cancellationToken);
        await PersistExistingRunRecordCollectionAsync(
            layout.RunUsageRoot(runId),
            plan.PreviousDetail.UsageObservations,
            plan.TargetDetail.UsageObservations,
            item => item.Id,
            item => $"{item.Id:N}.json",
            runId,
            "provider usage observation",
            EqualityComparer<Guid>.Default,
            cancellationToken);
        await PersistExistingRunRecordCollectionAsync(
            layout.RunArtifactsRoot(runId),
            plan.PreviousDetail.Artifacts,
            plan.TargetDetail.Artifacts,
            item => item.Id,
            item => $"{item.Id:N}.json",
            runId,
            "execution artifact",
            EqualityComparer<Guid>.Default,
            cancellationToken);
        await PersistExistingRunRecordCollectionAsync(
            layout.RunWorkflowCheckpointsRoot(runId),
            plan.PreviousDetail.Checkpoints,
            plan.TargetDetail.Checkpoints,
            item => item.Id,
            item => $"{item.Id:N}.json",
            runId,
            "execution checkpoint",
            EqualityComparer<Guid>.Default,
            cancellationToken);
        await PersistExistingRunRecordCollectionAsync(
            layout.RunReceiptsRoot(runId),
            plan.PreviousDetail.ToolReceipts,
            plan.TargetDetail.ToolReceipts,
            item => item.Id,
            item => $"{item.Id:N}.json",
            runId,
            "tool execution receipt",
            EqualityComparer<Guid>.Default,
            cancellationToken);
    }

    public async Task PersistExistingRunExecutionIndexAsync(
        ExistingExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        var currentIndex = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
            layout.ExecutionIndexPath,
            cancellationToken);
        EnsureStoredPayloadIsTransitionCompatible(
            currentIndex,
            plan.PreviousIndex,
            plan.TargetIndex,
            plan.TargetDetail.Run.Id,
            "execution index");
        await jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionIndexPath,
            plan.TargetIndex,
            cancellationToken);
    }

    public async Task PersistExistingRunUsageIndexAsync(
        ExistingExecutionRunPersistencePlan plan,
        CancellationToken cancellationToken)
    {
        var currentProjection = await jsonStore.ReadJsonAsync<AgentUsageProjection>(
            layout.ExecutionUsageIndexPath,
            cancellationToken);
        EnsureStoredPayloadIsTransitionCompatible(
            currentProjection,
            plan.PreviousUsageProjection,
            plan.TargetUsageProjection,
            plan.TargetDetail.Run.Id,
            "usage index");
        await jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionUsageIndexPath,
            plan.TargetUsageProjection,
            cancellationToken);
    }

    public void ValidateExistingRunUpdatePlan(
        ExistingExecutionRunPersistencePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.PreviousDetail);
        ArgumentNullException.ThrowIfNull(plan.TargetDetail);
        ArgumentNullException.ThrowIfNull(plan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(plan.TargetIndex);
        ArgumentNullException.ThrowIfNull(plan.PreviousUsageProjection);
        ArgumentNullException.ThrowIfNull(plan.TargetUsageProjection);

        var previousDetail = NormalizeRunDetail(plan.PreviousDetail);
        var targetDetail = NormalizeRunDetail(plan.TargetDetail);
        EnsureRunDetailConsistency(previousDetail);
        EnsureRunDetailConsistency(targetDetail);
        EnsureExistingRunIdentityIsStable(previousDetail, targetDetail);
        if (!HasSamePayload(previousDetail, plan.PreviousDetail) ||
            !HasSamePayload(targetDetail, plan.TargetDetail))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{targetDetail.Run.Id:N}' contains a non-normalized detail.");
        }

        if (HasSamePayload(previousDetail, targetDetail))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{targetDetail.Run.Id:N}' does not change its persisted payload.");
        }

        var expectedTargetIndex = CreateExistingRunTargetIndex(
            plan.PreviousIndex,
            previousDetail,
            targetDetail,
            plan.TargetIndex.UpdatedAtUtc);
        if (!HasSamePayload(expectedTargetIndex, plan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{targetDetail.Run.Id:N}' contains an invalid target execution index.");
        }

        if (plan.PreviousUsageProjection.Revision !=
                plan.PreviousIndex.Revision ||
            plan.TargetUsageProjection.Revision !=
                plan.TargetIndex.Revision)
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{targetDetail.Run.Id:N}' contains an invalid usage projection revision.");
        }

        var expectedTargetUsageProjection = ApplyUsageProjectionDelta(
            plan.PreviousUsageProjection,
            previousDetail,
            targetDetail,
            plan.TargetIndex);
        if (!HasSamePayload(
                expectedTargetUsageProjection,
                plan.TargetUsageProjection))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{targetDetail.Run.Id:N}' contains an invalid target usage projection.");
        }
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
            executionState.ExecutionLog,
            executionState.Metrics,
            executionState.ProviderUsageObservations);
        var nextChatIndex = new ExecutionChatIndex(
            Version: nextIndex.Version,
            Revision: nextIndex.Revision,
            UpdatedAtUtc: nextIndex.UpdatedAtUtc,
            SessionSummaries: projection.SessionSummaries,
            RunSummaries: projection.RunSummaries,
            ReportingProjectionVersion: WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion);

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

    private static ExecutionStorageIndex CreateNewRunTargetIndex(
        ExecutionStorageIndex currentIndex,
        ExecutionRunDetail detail,
        bool sessionExistedBefore,
        DateTimeOffset updatedAtUtc)
    {
        return new ExecutionStorageIndex(
            Version: string.IsNullOrWhiteSpace(currentIndex.Version) ? "3.0" : currentIndex.Version,
            Revision: currentIndex.Revision + 1L,
            UpdatedAtUtc: updatedAtUtc,
            SessionCount: currentIndex.SessionCount + (sessionExistedBefore ? 0 : 1),
            RunCount: currentIndex.RunCount + 1,
            LogCount: currentIndex.LogCount + detail.ExecutionLog.Count,
            MetricCount: currentIndex.MetricCount + detail.Metrics.Count,
            ApprovalCount: currentIndex.ApprovalCount + detail.Approvals.Count,
            ArtifactCount: currentIndex.ArtifactCount + detail.Artifacts.Count,
            CheckpointCount: currentIndex.CheckpointCount + detail.Checkpoints.Count,
            ReceiptCount: currentIndex.ReceiptCount + detail.ToolReceipts.Count,
            ActiveRunCount:
                currentIndex.ActiveRunCount +
                CountIndexedActiveRuns(detail.Run),
            FailedRunCount: currentIndex.FailedRunCount + CountIndexedFailedRuns(detail.Run),
            UsageObservationCount: currentIndex.UsageObservationCount + detail.UsageObservations.Count);
    }

    private static ExecutionStorageIndex CreateExistingRunTargetIndex(
        ExecutionStorageIndex currentIndex,
        ExecutionRunDetail previousDetail,
        ExecutionRunDetail targetDetail,
        DateTimeOffset updatedAtUtc)
    {
        return new ExecutionStorageIndex(
            Version: string.IsNullOrWhiteSpace(currentIndex.Version) ? "3.0" : currentIndex.Version,
            Revision: currentIndex.Revision + 1L,
            UpdatedAtUtc: updatedAtUtc,
            SessionCount: currentIndex.SessionCount,
            RunCount: currentIndex.RunCount,
            LogCount: currentIndex.LogCount + targetDetail.ExecutionLog.Count - previousDetail.ExecutionLog.Count,
            MetricCount: currentIndex.MetricCount + targetDetail.Metrics.Count - previousDetail.Metrics.Count,
            ApprovalCount: currentIndex.ApprovalCount + targetDetail.Approvals.Count - previousDetail.Approvals.Count,
            ArtifactCount: currentIndex.ArtifactCount + targetDetail.Artifacts.Count - previousDetail.Artifacts.Count,
            CheckpointCount: currentIndex.CheckpointCount + targetDetail.Checkpoints.Count - previousDetail.Checkpoints.Count,
            ReceiptCount: currentIndex.ReceiptCount + targetDetail.ToolReceipts.Count - previousDetail.ToolReceipts.Count,
            ActiveRunCount:
                currentIndex.ActiveRunCount +
                CountIndexedActiveRuns(targetDetail.Run) -
                CountIndexedActiveRuns(previousDetail.Run),
            FailedRunCount:
                currentIndex.FailedRunCount +
                CountIndexedFailedRuns(targetDetail.Run) -
                CountIndexedFailedRuns(previousDetail.Run),
            UsageObservationCount:
                currentIndex.UsageObservationCount +
                targetDetail.UsageObservations.Count -
                previousDetail.UsageObservations.Count);
    }

    private static ExecutionStorageIndex CreateAgentDeletionTargetIndex(
        ExecutionStorageIndex currentIndex,
        IReadOnlyList<ExecutionRunDetail> runDetails,
        int sessionCount,
        int orphanLogCount,
        int orphanMetricCount,
        int orphanUsageCount,
        int orphanApprovalCount,
        int orphanArtifactCount,
        int orphanReceiptCount,
        DateTimeOffset updatedAtUtc)
    {
        var runLogCount = runDetails.Sum(item => item.ExecutionLog.Count);
        var runMetricCount = runDetails.Sum(item => item.Metrics.Count);
        var runUsageCount = runDetails.Sum(item => item.UsageObservations.Count);
        var runApprovalCount = runDetails.Sum(item => item.Approvals.Count);
        var runArtifactCount = runDetails.Sum(item => item.Artifacts.Count);
        var runCheckpointCount = runDetails.Sum(item => item.Checkpoints.Count);
        var runReceiptCount = runDetails.Sum(item => item.ToolReceipts.Count);

        return currentIndex with
        {
            Revision = currentIndex.Revision + 1L,
            UpdatedAtUtc = updatedAtUtc,
            SessionCount = SubtractIndexedCount(currentIndex.SessionCount, sessionCount, "session"),
            RunCount = SubtractIndexedCount(currentIndex.RunCount, runDetails.Count, "execution run"),
            LogCount = SubtractIndexedCount(currentIndex.LogCount, runLogCount + orphanLogCount, "execution log"),
            MetricCount = SubtractIndexedCount(currentIndex.MetricCount, runMetricCount + orphanMetricCount, "execution metric"),
            ApprovalCount = SubtractIndexedCount(currentIndex.ApprovalCount, runApprovalCount + orphanApprovalCount, "execution approval"),
            ArtifactCount = SubtractIndexedCount(currentIndex.ArtifactCount, runArtifactCount + orphanArtifactCount, "execution artifact"),
            CheckpointCount = SubtractIndexedCount(currentIndex.CheckpointCount, runCheckpointCount, "execution checkpoint"),
            ReceiptCount = SubtractIndexedCount(currentIndex.ReceiptCount, runReceiptCount + orphanReceiptCount, "tool execution receipt"),
            ActiveRunCount = SubtractIndexedCount(
                currentIndex.ActiveRunCount,
                runDetails.Count(item => IsIndexedActiveRun(item.Run)),
                "active execution run"),
            FailedRunCount = SubtractIndexedCount(
                currentIndex.FailedRunCount,
                runDetails.Count(item => IsIndexedFailedRun(item.Run)),
                "failed execution run"),
            UsageObservationCount = SubtractIndexedCount(
                currentIndex.UsageObservationCount,
                runUsageCount + orphanUsageCount,
                "provider usage observation")
        };
    }

    private static int SubtractIndexedCount(int current, int removed, string label)
    {
        if (removed < 0 || removed > current)
        {
            throw new InvalidDataException(
                $"Agent deletion would make the canonical {label} count invalid.");
        }

        return current - removed;
    }

    private static bool IsIndexedActiveRun(ExecutionRunRecord run)
    {
        return IsIndexedActiveState(run.State);
    }

    private static bool IsIndexedActiveState(ExecutionState state)
    {
        return state is
            ExecutionState.Preparing or
            ExecutionState.Running or
            ExecutionState.WaitingOnTool or
            ExecutionState.Persisting;
    }

    private static bool IsIndexedFailedRun(ExecutionRunRecord run)
    {
        return run.Outcome == RunOutcome.Failed;
    }

    private async Task<AgentUsageProjection> LoadOrBuildUsageProjectionAsync(CancellationToken cancellationToken)
    {
        var executionIndex = await ResolveExecutionIndexAsync(cancellationToken);
        return await LoadOrBuildUsageProjectionAsync(
            executionIndex,
            cancellationToken);
    }

    private async Task<AgentUsageProjection> LoadOrBuildUsageProjectionAsync(
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
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
            .ToDictionary(
                item => CreateProviderKey(
                    item.ProviderName,
                    item.ProviderKind),
                ProviderUsageProjectionKeyComparer.Instance);
        var modelRows = currentProjection.Models
            .Select(CreateModelAccumulator)
            .ToDictionary(
                item => CreateModelKey(
                    item.ProviderName,
                    item.ProviderKind,
                    item.Model),
                ModelUsageProjectionKeyComparer.Instance);

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

    private static AgentUsageProjection CreateAgentDeletionUsageProjection(
        AgentUsageProjection currentProjection,
        IReadOnlyList<ExecutionRunDetail> runDetails,
        IReadOnlyList<ProviderUsageObservation> orphanUsage,
        ExecutionStorageIndex targetIndex)
    {
        var agentRows = currentProjection.Agents
            .Select(CreateAgentAccumulator)
            .ToDictionary(item => item.AgentId);
        var providerRows = currentProjection.Providers
            .Select(CreateProviderAccumulator)
            .ToDictionary(
                item => CreateProviderKey(item.ProviderName, item.ProviderKind),
                ProviderUsageProjectionKeyComparer.Instance);
        var modelRows = currentProjection.Models
            .Select(CreateModelAccumulator)
            .ToDictionary(
                item => CreateModelKey(item.ProviderName, item.ProviderKind, item.Model),
                ModelUsageProjectionKeyComparer.Instance);

        foreach (var detail in runDetails)
        {
            SubtractRunContribution(agentRows, providerRows, modelRows, detail);
        }

        foreach (var observation in orphanUsage)
        {
            SubtractProviderUsage(providerRows, observation);
            SubtractModelUsage(modelRows, observation);
        }

        return new AgentUsageProjection(
            Version: string.IsNullOrWhiteSpace(targetIndex.Version)
                ? currentProjection.Version
                : targetIndex.Version,
            Revision: targetIndex.Revision,
            UpdatedAtUtc: targetIndex.UpdatedAtUtc,
            Agents: OrderAgentRows(agentRows.Values),
            Providers: OrderProviderRows(providerRows.Values),
            Models: OrderModelRows(modelRows.Values));
    }

    private async Task<AgentUsageProjection> CreateAgentDeletionUsageProjectionAsync(
        Guid deletedAgentId,
        AgentUsageProjection currentProjection,
        IReadOnlyList<ExecutionRunDetail> runDetails,
        IReadOnlyList<ProviderUsageObservation> orphanUsage,
        ExecutionStorageIndex targetIndex,
        CancellationToken cancellationToken)
    {
        var targetProjection = CreateAgentDeletionUsageProjection(
            currentProjection,
            runDetails,
            orphanUsage,
            targetIndex);
        if (!RequiresAgentDeletionUsageRebuild(
                deletedAgentId,
                currentProjection,
                targetProjection,
                runDetails,
                orphanUsage))
        {
            return targetProjection;
        }

        var deletedRunIds = runDetails
            .Select(item => item.Run.Id)
            .ToHashSet();
        var deletedUsageIds = runDetails
            .SelectMany(item => item.UsageObservations)
            .Concat(orphanUsage)
            .Select(item => item.Id)
            .ToHashSet();
        var source = await LoadUsageProjectionSourceAsync(cancellationToken);
        var targetSource = source with
        {
            ExecutionRuns = source.ExecutionRuns
                .Where(item => !deletedRunIds.Contains(item.Id))
                .ToList(),
            ProviderUsageObservations = source.ProviderUsageObservations
                .Where(item => !deletedUsageIds.Contains(item.Id))
                .ToList()
        };
        return BuildUsageProjection(targetSource, targetIndex);
    }

    private static bool RequiresAgentDeletionUsageRebuild(
        Guid deletedAgentId,
        AgentUsageProjection currentProjection,
        AgentUsageProjection targetProjection,
        IReadOnlyList<ExecutionRunDetail> runDetails,
        IReadOnlyList<ProviderUsageObservation> orphanUsage)
    {
        var targetAgentIds = targetProjection.Agents
            .Select(item => item.AgentId)
            .ToHashSet();
        foreach (var detail in runDetails)
        {
            var currentAgentLastUsedAtUtc = currentProjection.Agents
                .FirstOrDefault(item => item.AgentId == detail.Run.AgentId)?
                .LastUsedAtUtc;
            if (detail.Run.AgentId != deletedAgentId &&
                targetAgentIds.Contains(detail.Run.AgentId) &&
                (currentAgentLastUsedAtUtc == detail.Run.UpdatedAtUtc ||
                 detail.UsageObservations.Any(
                     item => item.CreatedAtUtc == currentAgentLastUsedAtUtc)))
            {
                return true;
            }
        }

        var deletedUsage = runDetails
            .SelectMany(item => item.UsageObservations)
            .Concat(orphanUsage)
            .ToList();
        foreach (var observation in deletedUsage)
        {
            var providerKey = CreateProviderKey(
                observation.ProviderName,
                observation.ProviderKind);
            var currentProvider = currentProjection.Providers.FirstOrDefault(item =>
                ProviderUsageProjectionKeyComparer.Instance.Equals(
                    CreateProviderKey(item.ProviderName, item.ProviderKind),
                    providerKey));
            var targetProviderExists = targetProjection.Providers.Any(item =>
                ProviderUsageProjectionKeyComparer.Instance.Equals(
                    CreateProviderKey(item.ProviderName, item.ProviderKind),
                    providerKey));
            if (targetProviderExists &&
                currentProvider?.LastUsedAtUtc == observation.CreatedAtUtc)
            {
                return true;
            }

            var modelKey = CreateModelKey(
                observation.ProviderName,
                observation.ProviderKind,
                observation.Model);
            var currentModel = currentProjection.Models.FirstOrDefault(item =>
                ModelUsageProjectionKeyComparer.Instance.Equals(
                    CreateModelKey(item.ProviderName, item.ProviderKind, item.Model),
                    modelKey));
            var targetModelExists = targetProjection.Models.Any(item =>
                ModelUsageProjectionKeyComparer.Instance.Equals(
                    CreateModelKey(item.ProviderName, item.ProviderKind, item.Model),
                    modelKey));
            if (targetModelExists &&
                currentModel?.LastUsedAtUtc == observation.CreatedAtUtc)
            {
                return true;
            }
        }

        return false;
    }

    private static AgentUsageProjection BuildUsageProjection(
        SandboxWorkspaceExecutionState executionState,
        ExecutionStorageIndex executionIndex)
    {
        var agentRows = new Dictionary<Guid, AgentUsageProjectionAccumulator>();
        var providerRows = new Dictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator>(
            ProviderUsageProjectionKeyComparer.Instance);
        var modelRows = new Dictionary<
            ModelUsageProjectionKey,
            ModelUsageProjectionAccumulator>(
            ModelUsageProjectionKeyComparer.Instance);
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
        IDictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator> providerRows,
        IDictionary<
            ModelUsageProjectionKey,
            ModelUsageProjectionAccumulator> modelRows,
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
        IDictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator> providerRows,
        IDictionary<
            ModelUsageProjectionKey,
            ModelUsageProjectionAccumulator> modelRows,
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
        IDictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator> providerRows,
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
        IDictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator> providerRows,
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
        IDictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator> providerRows,
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
        IDictionary<
            ModelUsageProjectionKey,
            ModelUsageProjectionAccumulator> modelRows,
        ProviderUsageObservation observation)
    {
        var key = CreateModelKey(observation.ProviderName, observation.ProviderKind, observation.Model);
        var row = GetOrAddModel(modelRows, observation.ProviderName, observation.ProviderKind, observation.Model);
        ApplyUsage(row, observation, delta: 1);
        row.LastUsedAtUtc = MaxDate(row.LastUsedAtUtc, observation.CreatedAtUtc);
        modelRows[key] = row;
    }

    private static void SubtractModelUsage(
        IDictionary<
            ModelUsageProjectionKey,
            ModelUsageProjectionAccumulator> modelRows,
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
        IDictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator> rows,
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
        IDictionary<
            ModelUsageProjectionKey,
            ModelUsageProjectionAccumulator> rows,
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

    private static ProviderUsageProjectionKey CreateProviderKey(
        string providerName,
        ProviderKind providerKind)
    {
        return new ProviderUsageProjectionKey(
            providerKind,
            NormalizeProviderName(providerName));
    }

    private static ModelUsageProjectionKey CreateModelKey(
        string providerName,
        ProviderKind providerKind,
        string model)
    {
        return new ModelUsageProjectionKey(
            providerKind,
            NormalizeProviderName(providerName),
            NormalizeModel(model));
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
        IDictionary<
            ProviderUsageProjectionKey,
            ProviderUsageProjectionAccumulator> rows,
        ProviderUsageProjectionKey key,
        ProviderUsageProjectionAccumulator row)
    {
        if (row.IsEmpty)
        {
            rows.Remove(key);
        }
    }

    private static void RemoveIfEmpty(
        IDictionary<
            ModelUsageProjectionKey,
            ModelUsageProjectionAccumulator> rows,
        ModelUsageProjectionKey key,
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

    private async Task PersistExistingRunRecordCollectionAsync<T, TKey>(
        string directoryPath,
        IReadOnlyList<T> previousRecords,
        IReadOnlyList<T> targetRecords,
        Func<T, TKey> keySelector,
        Func<T, string> fileNameSelector,
        Guid executionRunId,
        string label,
        IEqualityComparer<TKey> keyComparer,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        var storedRecords = await jsonStore.LoadRecordsFromDirectoryAsync<T>(
            directoryPath,
            cancellationToken);
        EnsureStoredRecordsAreTransitionCompatible(
            storedRecords,
            previousRecords,
            targetRecords,
            keySelector,
            executionRunId,
            label,
            keyComparer);
        await jsonStore.PersistRecordDirectoryDiffAsync(
            directoryPath,
            storedRecords,
            targetRecords,
            fileNameSelector,
            cancellationToken);
    }

    private void EnsureStoredPayloadIsTransitionCompatible<T>(
        T? stored,
        T? previous,
        T? target,
        Guid executionRunId,
        string label)
        where T : class
    {
        if (stored is not null &&
            (previous is null || !HasSamePayload(stored, previous)) &&
            (target is null || !HasSamePayload(stored, target)))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{executionRunId:N}' found an unexpected {label} payload.");
        }

        if (stored is null &&
            previous is not null &&
            target is not null)
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{executionRunId:N}' found a missing {label} payload.");
        }
    }

    private void EnsureStoredRecordsAreTransitionCompatible<T, TKey>(
        IReadOnlyList<T> storedRecords,
        IReadOnlyList<T> previousRecords,
        IReadOnlyList<T> targetRecords,
        Func<T, TKey> keySelector,
        Guid executionRunId,
        string label,
        IEqualityComparer<TKey> keyComparer)
        where TKey : notnull
    {
        var previousByKey = previousRecords.ToDictionary(
            keySelector,
            keyComparer);
        var targetByKey = targetRecords.ToDictionary(
            keySelector,
            keyComparer);
        var storedKeys = new HashSet<TKey>(keyComparer);

        foreach (var storedRecord in storedRecords)
        {
            var key = keySelector(storedRecord);
            if (!storedKeys.Add(key))
            {
                throw new InvalidDataException(
                    $"Pending execution-run update '{executionRunId:N}' found duplicate {label} records.");
            }

            var matchesPrevious =
                previousByKey.TryGetValue(key, out var previousRecord) &&
                HasSamePayload(storedRecord, previousRecord);
            var matchesTarget =
                targetByKey.TryGetValue(key, out var targetRecord) &&
                HasSamePayload(storedRecord, targetRecord);
            if (!matchesPrevious && !matchesTarget)
            {
                throw new InvalidDataException(
                    $"Pending execution-run update '{executionRunId:N}' found a conflicting {label} record.");
            }
        }
    }

    private static void EnsureExistingRunIdentityIsStable(
        ExecutionRunDetail previousDetail,
        ExecutionRunDetail targetDetail)
    {
        if (previousDetail.Run.Id == Guid.Empty ||
            previousDetail.Run.Id != targetDetail.Run.Id)
        {
            throw new InvalidDataException(
                "A pending execution-run update has an invalid execution-run identity.");
        }

        if (previousDetail.Run.AgentId != targetDetail.Run.AgentId ||
            previousDetail.Run.ChatSessionId != targetDetail.Run.ChatSessionId)
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{targetDetail.Run.Id:N}' cannot change the run's agent or chat-session identity.");
        }

        if (targetDetail.Run.UpdatedAtUtc <
                previousDetail.Run.UpdatedAtUtc ||
            !string.Equals(
                previousDetail.Run.ProviderName,
                targetDetail.Run.ProviderName,
                StringComparison.Ordinal) ||
            !string.Equals(
                previousDetail.Run.Model,
                targetDetail.Run.Model,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{targetDetail.Run.Id:N}' cannot move its update timestamp backwards or change its provider/model identity.");
        }

        EnsureUsageObservationTransition(
            previousDetail.Run.Id,
            previousDetail.UsageObservations,
            targetDetail.UsageObservations);
    }

    private static void EnsureUsageObservationTransition(
        Guid executionRunId,
        IReadOnlyList<ProviderUsageObservation> previous,
        IReadOnlyList<ProviderUsageObservation> target)
    {
        Dictionary<Guid, ProviderUsageObservation> targetById;
        try
        {
            targetById = target.ToDictionary(observation => observation.Id);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{executionRunId:N}' contains duplicate provider usage observation identities.",
                exception);
        }

        var previousIds = new HashSet<Guid>();
        foreach (var prior in previous)
        {
            if (!previousIds.Add(prior.Id))
            {
                throw new InvalidDataException(
                    $"Pending execution-run update '{executionRunId:N}' contains duplicate prior provider usage observation identities.");
            }

            if (!targetById.TryGetValue(prior.Id, out var current))
            {
                throw new InvalidDataException(
                    $"Pending execution-run update '{executionRunId:N}' cannot remove provider usage observation '{prior.Id:N}'.");
            }

            if (prior.CreatedAtUtc != current.CreatedAtUtc ||
                prior.ProviderKind != current.ProviderKind ||
                prior.TransportKind != current.TransportKind ||
                prior.ExecutionRunId != current.ExecutionRunId ||
                prior.AgentId != current.AgentId ||
                prior.ChatSessionId != current.ChatSessionId ||
                !string.Equals(
                    prior.ProviderName,
                    current.ProviderName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    prior.Model,
                    current.Model,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Pending execution-run update '{executionRunId:N}' cannot change the identity, timestamp, provider, or model of provider usage observation '{prior.Id:N}'.");
            }
        }
    }

    private readonly record struct ProviderUsageProjectionKey(
        ProviderKind ProviderKind,
        string ProviderName);

    private readonly record struct ModelUsageProjectionKey(
        ProviderKind ProviderKind,
        string ProviderName,
        string Model);

    private sealed class ProviderUsageProjectionKeyComparer :
        IEqualityComparer<ProviderUsageProjectionKey>
    {
        public static ProviderUsageProjectionKeyComparer Instance { get; } =
            new();

        public bool Equals(
            ProviderUsageProjectionKey left,
            ProviderUsageProjectionKey right)
        {
            return left.ProviderKind == right.ProviderKind &&
                   string.Equals(
                       left.ProviderName,
                       right.ProviderName,
                       StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ProviderUsageProjectionKey value)
        {
            return HashCode.Combine(
                value.ProviderKind,
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    value.ProviderName));
        }
    }

    private sealed class ModelUsageProjectionKeyComparer :
        IEqualityComparer<ModelUsageProjectionKey>
    {
        public static ModelUsageProjectionKeyComparer Instance { get; } =
            new();

        public bool Equals(
            ModelUsageProjectionKey left,
            ModelUsageProjectionKey right)
        {
            return left.ProviderKind == right.ProviderKind &&
                   string.Equals(
                       left.ProviderName,
                       right.ProviderName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       left.Model,
                       right.Model,
                       StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ModelUsageProjectionKey value)
        {
            return HashCode.Combine(
                value.ProviderKind,
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    value.ProviderName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    value.Model));
        }
    }

    private bool HasSamePayload<T>(T left, T right)
    {
        return !jsonStore.RequiresSave(left, right);
    }

    private void EnsurePersistedDetailIsCompatible(
        ExecutionRunDetail persisted,
        ExecutionRunDetail target)
    {
        if (!HasSamePayload(persisted.Run, target.Run))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{target.Run.Id:N}' found a conflicting execution run.");
        }

        if (persisted.ChatSession is null ||
            target.ChatSession is null ||
            !HasSamePayload(persisted.ChatSession, target.ChatSession))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{target.Run.Id:N}' found a conflicting chat session.");
        }

        EnsurePersistedRecordsAreSubset(
            persisted.ExecutionLog,
            target.ExecutionLog,
            item => item.Id,
            target.Run.Id,
            "execution log");
        EnsurePersistedRecordsAreSubset(
            persisted.Metrics,
            target.Metrics,
            item => item.Id,
            target.Run.Id,
            "execution metric");
        EnsurePersistedRecordsAreSubset(
            persisted.UsageObservations,
            target.UsageObservations,
            item => item.Id,
            target.Run.Id,
            "provider usage observation");
        EnsurePersistedRecordsAreSubset(
            persisted.Approvals,
            target.Approvals,
            item => item.ApprovalId,
            target.Run.Id,
            "execution approval");
        EnsurePersistedRecordsAreSubset(
            persisted.Artifacts,
            target.Artifacts,
            item => item.Id,
            target.Run.Id,
            "execution artifact");
        EnsurePersistedRecordsAreSubset(
            persisted.Checkpoints,
            target.Checkpoints,
            item => item.Id,
            target.Run.Id,
            "execution checkpoint");
        EnsurePersistedRecordsAreSubset(
            persisted.ToolReceipts,
            target.ToolReceipts,
            item => item.Id,
            target.Run.Id,
            "tool execution receipt");
    }

    private void EnsurePersistedGenericNewRunDetailIsCompatible(
        ExecutionRunDetail persisted,
        ExecutionRunDetail target)
    {
        if (!HasSamePayload(persisted.Run, target.Run))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{target.Run.Id:N}' found a conflicting execution run.");
        }

        if ((persisted.ChatSession is null) !=
                (target.ChatSession is null) ||
            persisted.ChatSession is not null &&
            target.ChatSession is not null &&
            !HasSamePayload(
                persisted.ChatSession,
                target.ChatSession))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{target.Run.Id:N}' found a conflicting chat session.");
        }

        EnsurePersistedRecordsAreSubset(
            persisted.ExecutionLog,
            target.ExecutionLog,
            item => item.Id,
            target.Run.Id,
            "execution log");
        EnsurePersistedRecordsAreSubset(
            persisted.Metrics,
            target.Metrics,
            item => item.Id,
            target.Run.Id,
            "execution metric");
        EnsurePersistedRecordsAreSubset(
            persisted.UsageObservations,
            target.UsageObservations,
            item => item.Id,
            target.Run.Id,
            "provider usage observation");
        EnsurePersistedRecordsAreSubset(
            persisted.Approvals,
            target.Approvals,
            item => item.ApprovalId,
            target.Run.Id,
            "execution approval");
        EnsurePersistedRecordsAreSubset(
            persisted.Artifacts,
            target.Artifacts,
            item => item.Id,
            target.Run.Id,
            "execution artifact");
        EnsurePersistedRecordsAreSubset(
            persisted.Checkpoints,
            target.Checkpoints,
            item => item.Id,
            target.Run.Id,
            "execution checkpoint");
        EnsurePersistedRecordsAreSubset(
            persisted.ToolReceipts,
            target.ToolReceipts,
            item => item.Id,
            target.Run.Id,
            "tool execution receipt");
    }

    private void EnsurePersistedRecordsAreSubset<T, TKey>(
        IReadOnlyList<T> persisted,
        IReadOnlyList<T> target,
        Func<T, TKey> keySelector,
        Guid executionRunId,
        string label)
        where TKey : notnull
    {
        var targetByKey = target.ToDictionary(keySelector);
        foreach (var item in persisted)
        {
            var key = keySelector(item);
            if (!targetByKey.TryGetValue(key, out var targetItem) ||
                !HasSamePayload(item, targetItem))
            {
                throw new InvalidDataException(
                    $"Pending chat-run transaction '{executionRunId:N}' found a conflicting {label} record.");
            }
        }
    }

    private static bool MatchesDeletedAgent(
        Guid recordAgentId,
        Guid? chatSessionId,
        Guid executionRunId,
        Guid agentId,
        IReadOnlySet<Guid> sessionIds,
        IReadOnlySet<Guid> runIds)
    {
        return recordAgentId == agentId ||
               chatSessionId.HasValue && sessionIds.Contains(chatSessionId.Value) ||
               executionRunId != Guid.Empty && runIds.Contains(executionRunId);
    }

    private void DeleteRunRoot(Guid executionRunId)
    {
        var runsRoot = Path.GetFullPath(layout.ExecutionRunsRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var runRoot = Path.GetFullPath(layout.RunRoot(executionRunId));
        if (!runRoot.StartsWith(runsRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Execution run deletion target '{runRoot}' is outside the canonical runs root.");
        }

        if (Directory.Exists(runRoot))
        {
            Directory.Delete(runRoot, recursive: true);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

internal sealed record AgentExecutionDeletionPlan(
    Guid AgentId,
    IReadOnlyList<Guid> RunIds,
    IReadOnlyList<Guid> SessionIds,
    IReadOnlyList<ExecutionLogEntry> OrphanLogs,
    IReadOnlyList<AgentRunMetric> OrphanMetrics,
    IReadOnlyList<ProviderUsageObservation> OrphanUsage,
    IReadOnlyList<ExecutionApprovalRecord> OrphanApprovals,
    IReadOnlyList<ExecutionArtifactRecord> OrphanArtifacts,
    IReadOnlyList<ToolExecutionReceiptRecord> OrphanReceipts,
    ExecutionStorageIndex SourceIndex,
    ExecutionStorageIndex TargetIndex,
    AgentUsageProjection? TargetUsageProjection,
    AgentUsageProjection? SourceUsageProjection,
    bool HasExecutionChanges);

internal sealed record NewExecutionRunPersistencePlan(
    ChatSessionRecord? PreviousSession,
    ExecutionRunDetail Detail,
    ExecutionStorageIndex PreviousIndex,
    ExecutionStorageIndex TargetIndex);

internal sealed record GenericNewExecutionRunPersistencePlan(
    ChatSessionRecord? PreviousSession,
    ExecutionRunDetail Detail,
    ExecutionStorageIndex PreviousIndex,
    ExecutionStorageIndex TargetIndex,
    AgentUsageProjection PreviousUsageProjection,
    AgentUsageProjection TargetUsageProjection);

internal sealed record ExistingExecutionRunPersistencePlan(
    ExecutionRunDetail PreviousDetail,
    ExecutionRunDetail TargetDetail,
    ExecutionStorageIndex PreviousIndex,
    ExecutionStorageIndex TargetIndex,
    AgentUsageProjection PreviousUsageProjection,
    AgentUsageProjection TargetUsageProjection);

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
    IReadOnlyList<ChatRunSummaryRecord> RunSummaries,
    int ReportingProjectionVersion = 0);

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
