using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceChatProjectionStore(
    FileSandboxWorkspaceStorageLayout layout,
    FileSandboxWorkspaceJsonStore jsonStore)
{
    private const int ReportingUpgradeReadConcurrency = 4;
    private ExecutionChatIndex? cachedReportingChatIndex;
    private ReportingIndexFileStamp? cachedReportingIndexStamp;
    private ReportingIndexFileStamp? cachedReportingExecutionIndexStamp;

    public async Task<ChatWorkspaceProjectionSnapshot> LoadChatWorkspaceProjectionAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return new ChatWorkspaceProjectionSnapshot(
            chatIndex.SessionSummaries
                .Where(item => item.AgentId == agentId)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            chatIndex.RunSummaries
                .Where(item => item.AgentId == agentId)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList());
    }

    public async Task<IReadOnlyList<ChatSessionSummaryRecord>> ListChatSessionSummariesAsync(
        Guid? agentId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return chatIndex.SessionSummaries
            .Where(item => !agentId.HasValue || item.AgentId == agentId.Value)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public Task<ChatSessionRecord?> GetChatSessionAsync(
        Guid chatSessionId,
        CancellationToken cancellationToken)
        => jsonStore.ReadJsonAsync<ChatSessionRecord>(layout.SessionPath(chatSessionId), cancellationToken);

    public async Task<IReadOnlyList<ChatRunSummaryRecord>> ListChatRunSummariesAsync(
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return chatIndex.RunSummaries
            .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatRunSummaryRecord>> ListAllRunSummariesAsync(CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        return chatIndex.RunSummaries
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public Task<ExecutionChatIndex> LoadExecutionReportIndexAsync(
        CancellationToken cancellationToken)
        => LoadOrBuildReportingChatIndexAsync(cancellationToken);

    public async Task<ExecutionChatIndex> LoadCurrentIndexAsync(
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionIndex);

        var current = await LoadOrBuildChatIndexAsync(cancellationToken);
        if (current.Revision == executionIndex.Revision &&
            current.SessionSummaries.Count == executionIndex.SessionCount &&
            current.RunSummaries.Count == executionIndex.RunCount)
        {
            return current;
        }

        var rebuilt = await BuildChatIndexAsync(cancellationToken);
        if (rebuilt.SessionSummaries.Count != executionIndex.SessionCount ||
            rebuilt.RunSummaries.Count != executionIndex.RunCount)
        {
            throw new InvalidDataException(
                "The execution chat index cannot be reconciled with the canonical execution index before agent deletion.");
        }

        rebuilt = rebuilt with
        {
            Revision = executionIndex.Revision,
            UpdatedAtUtc = executionIndex.UpdatedAtUtc
        };
        await jsonStore.WriteJsonAtomicallyAsync(
            layout.ExecutionChatIndexPath,
            rebuilt,
            cancellationToken);
        return rebuilt;
    }

    public Task PersistAgentDeletionIndexAsync(
        ExecutionChatIndex targetIndex,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetIndex);
        return jsonStore.WriteJsonIfChangedAsync(
            layout.ExecutionChatIndexPath,
            targetIndex,
            cancellationToken);
    }

    public async Task<ExecutionReportIndexPreparation> InspectExecutionReportIndexAsync(
        CancellationToken cancellationToken)
    {
        var currentStamp = ReadFileStamp(layout.ExecutionChatIndexPath);
        var currentExecutionIndexStamp = ReadFileStamp(layout.ExecutionIndexPath);
        if (cachedReportingChatIndex is not null &&
            currentStamp.HasValue &&
            currentStamp == cachedReportingIndexStamp &&
            currentExecutionIndexStamp == cachedReportingExecutionIndexStamp)
        {
            return ExecutionReportIndexPreparation.Ready(
                currentStamp,
                currentExecutionIndexStamp,
                cachedReportingChatIndex,
                requiresWrite: false);
        }

        var chatIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
            layout.ExecutionChatIndexPath,
            cancellationToken);
        var executionIndex = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
            layout.ExecutionIndexPath,
            cancellationToken);
        if (chatIndex is null || IsStale(chatIndex, executionIndex))
        {
            return ExecutionReportIndexPreparation.Pending(
                currentStamp,
                currentExecutionIndexStamp,
                chatIndex,
                executionIndex);
        }

        var requiresUpgrade = chatIndex.ReportingProjectionVersion <
            WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion;
        if (chatIndex.RunSummaries.Any(
                static summary =>
                    summary.ReportingProjectionVersion <
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion))
        {
            return ExecutionReportIndexPreparation.Pending(
                currentStamp,
                currentExecutionIndexStamp,
                chatIndex,
                executionIndex);
        }

        var targetIndex = PrepareCurrentReportingIndex(chatIndex, requiresUpgrade);
        return ExecutionReportIndexPreparation.Ready(
            currentStamp,
            currentExecutionIndexStamp,
            targetIndex,
            requiresWrite: !ReferenceEquals(targetIndex, chatIndex));
    }

    public async Task<ExecutionReportIndexPreparation> MaterializeExecutionReportIndexAsync(
        ExecutionReportIndexPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (!preparation.RequiresMaterialization)
        {
            return preparation;
        }

        var targetIndex = preparation.SourceIndex is null ||
                          IsStale(
                              preparation.SourceIndex,
                              preparation.ExecutionIndex)
            ? await BuildReportingChatIndexAsync(
                preparation.ExecutionIndex,
                cancellationToken)
            : await UpgradeReportingChatIndexAsync(
                preparation.SourceIndex,
                cancellationToken);
        return preparation.WithTarget(targetIndex);
    }

    public async Task<ExecutionChatIndex?> TryPublishExecutionReportIndexAsync(
        ExecutionReportIndexPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.TargetIndex is null)
        {
            throw new InvalidOperationException(
                "The agent execution reporting index must be materialized before it can be published.");
        }

        if (ReadFileStamp(layout.ExecutionChatIndexPath) != preparation.ChatIndexStamp ||
            ReadFileStamp(layout.ExecutionIndexPath) != preparation.ExecutionIndexStamp)
        {
            return null;
        }

        if (preparation.RequiresWrite)
        {
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                preparation.TargetIndex,
                cancellationToken);
        }

        return CacheReportingIndex(preparation.TargetIndex);
    }

    public async Task<AgentExecutionReportPage> QueryExecutionReportAsync(
        AgentExecutionReportQuery query,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadExecutionReportIndexAsync(cancellationToken);
        return QueryExecutionReport(chatIndex, query, cancellationToken);
    }

    public AgentExecutionReportPage QueryExecutionReport(
        ExecutionChatIndex chatIndex,
        AgentExecutionReportQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chatIndex);
        ArgumentNullException.ThrowIfNull(query);
        var normalized = NormalizeReportQuery(query);
        var pageItems = new List<ChatRunSummaryRecord>(normalized.PageSize);
        var dailyCost = new SortedDictionary<DateOnly, DailyCostAccumulator>();
        var matchingRunCount = 0;
        var knownCostUsd = 0m;
        var totalDurationTicks = 0L;
        var unknownCostRunCount = 0;
        var legacyProjectAttributionRunCount = 0;
        var invalidProjectAttributionRunCount = 0;
        var invalidCorrelationRunCount = 0;

        for (var index = 0; index < chatIndex.RunSummaries.Count; index++)
        {
            if ((index & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var run = chatIndex.RunSummaries[index];
            if (normalized.ActivityFromUtc.HasValue &&
                run.ActivityAtUtc < normalized.ActivityFromUtc.Value)
            {
                break;
            }

            if (normalized.IncludeAggregate &&
                (run.ProjectAttributionSource is
                    AgentExecutionProjectAttributionSource.InvalidRecordedScope or
                    AgentExecutionProjectAttributionSource.InvalidLegacySource) &&
                MatchesWithoutProjectAttribution(run, normalized))
            {
                invalidProjectAttributionRunCount++;
            }

            if (normalized.IncludeAggregate &&
                run.InvalidCorrelationIdCount > 0 &&
                MatchesWithoutProjectAttribution(run, normalized))
            {
                invalidCorrelationRunCount++;
            }

            if (!Matches(run, normalized))
            {
                continue;
            }

            if (matchingRunCount >= normalized.Offset &&
                pageItems.Count < normalized.PageSize)
            {
                pageItems.Add(run);
            }

            matchingRunCount++;
            if (!normalized.IncludeAggregate)
            {
                if (pageItems.Count == normalized.PageSize)
                {
                    break;
                }

                continue;
            }

            knownCostUsd += run.KnownCostUsd;
            AddDuration(ref totalDurationTicks, run.Duration);
            if (run.HasUnknownCost)
            {
                unknownCostRunCount++;
            }

            if (run.ProjectAttributionSource ==
                AgentExecutionProjectAttributionSource.LegacySource)
            {
                legacyProjectAttributionRunCount++;
            }

            AddDailyCost(dailyCost, run, normalized.DailyTrendFromUtc);
        }

        var trend = new List<AgentExecutionDailyCost>(dailyCost.Count);
        foreach (var (dayUtc, accumulator) in dailyCost)
        {
            trend.Add(new AgentExecutionDailyCost(
                dayUtc,
                accumulator.KnownCostUsd,
                accumulator.RunCount,
                accumulator.UnknownCostRunCount));
        }

        return new AgentExecutionReportPage(
            pageItems,
            normalized.PageIndex,
            normalized.PageSize,
            new AgentExecutionReportTotals(
                normalized.KnownTotalCount ?? matchingRunCount,
                knownCostUsd,
                TimeSpan.FromTicks(totalDurationTicks),
                unknownCostRunCount)
            {
                LegacyProjectAttributionRunCount =
                    legacyProjectAttributionRunCount,
                InvalidProjectAttributionRunCount =
                    invalidProjectAttributionRunCount,
                InvalidCorrelationRunCount = invalidCorrelationRunCount
            },
            trend);
    }

    public async Task<ChatRuntimeSnapshot> LoadChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        var chatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        var runSummaries = chatIndex.RunSummaries
            .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var executionLog = new List<ExecutionLogEntry>();
        var metrics = new List<AgentRunMetric>();

        foreach (var runSummary in runSummaries)
        {
            executionLog.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(
                layout.RunLogsRoot(runSummary.ExecutionRunId),
                cancellationToken));
            metrics.AddRange(await jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(
                layout.RunMetricsRoot(runSummary.ExecutionRunId),
                cancellationToken));
        }

        return new ChatRuntimeSnapshot(
            executionLog
                .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList(),
            metrics
                .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList());
    }

    public async Task<bool> SaveRunDetailAsync(
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(executionIndex);

        var currentChatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        var nextChatIndex = await CreateRunDetailChatIndexAsync(
            currentChatIndex,
            previousDetail,
            detail,
            executionIndex,
            cancellationToken);
        if (File.Exists(layout.ExecutionChatIndexPath) &&
            !jsonStore.RequiresSave(currentChatIndex, nextChatIndex))
        {
            return false;
        }

        await jsonStore.WriteJsonAtomicallyAsync(
            layout.ExecutionChatIndexPath,
            nextChatIndex,
            cancellationToken);
        return true;
    }

    public async Task<GenericNewExecutionRunChatProjectionPlan> PrepareGenericNewRunAsync(
        GenericNewExecutionRunPersistencePlan persistencePlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);

        var previousChatIndex = await LoadOrBuildChatIndexAsync(
            cancellationToken);
        var targetChatIndex = await CreateRunDetailChatIndexAsync(
            previousChatIndex,
            previousDetail: null,
            persistencePlan.Detail,
            persistencePlan.TargetIndex,
            cancellationToken);
        return new GenericNewExecutionRunChatProjectionPlan(
            previousChatIndex,
            targetChatIndex);
    }

    public async Task ValidateGenericNewRunPlanAsync(
        GenericNewExecutionRunPersistencePlan persistencePlan,
        GenericNewExecutionRunChatProjectionPlan chatProjectionPlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.TargetIndex);

        var expectedTarget = await CreateRunDetailChatIndexAsync(
            chatProjectionPlan.PreviousIndex,
            previousDetail: null,
            persistencePlan.Detail,
            persistencePlan.TargetIndex,
            cancellationToken);
        if (jsonStore.RequiresSave(expectedTarget, chatProjectionPlan.TargetIndex))
        {
            var expectedLegacyTarget = await CreateRunDetailChatIndexAsync(
                chatProjectionPlan.PreviousIndex,
                previousDetail: null,
                persistencePlan.Detail,
                persistencePlan.TargetIndex,
                cancellationToken,
                includeReportingProjection: false);
            if (jsonStore.RequiresSave(
                    expectedLegacyTarget,
                    chatProjectionPlan.TargetIndex))
            {
                throw new InvalidDataException(
                    $"Pending generic execution-run creation '{persistencePlan.Detail.Run.Id:N}' contains an invalid target chat index.");
            }
        }

        var currentChatIndex =
            await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                layout.ExecutionChatIndexPath,
                cancellationToken);
        if (currentChatIndex is null ||
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.PreviousIndex) &&
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{persistencePlan.Detail.Run.Id:N}' found an unexpected chat index.");
        }
    }

    public async Task PersistGenericNewRunAsync(
        GenericNewExecutionRunPersistencePlan persistencePlan,
        GenericNewExecutionRunChatProjectionPlan chatProjectionPlan,
        bool validatePersistedState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.TargetIndex);

        if (validatePersistedState)
        {
            var currentChatIndex =
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    cancellationToken);
            if (currentChatIndex is null ||
                jsonStore.RequiresSave(
                    currentChatIndex,
                    chatProjectionPlan.PreviousIndex) &&
                jsonStore.RequiresSave(
                    currentChatIndex,
                    chatProjectionPlan.TargetIndex))
            {
                throw new InvalidDataException(
                    $"Pending generic execution-run creation '{persistencePlan.Detail.Run.Id:N}' found an unexpected chat index.");
            }
        }

        if (validatePersistedState)
        {
            await jsonStore.WriteJsonIfChangedAsync(
                layout.ExecutionChatIndexPath,
                chatProjectionPlan.TargetIndex,
                cancellationToken);
            return;
        }

        await jsonStore.WriteJsonAtomicallyAsync(
            layout.ExecutionChatIndexPath,
            chatProjectionPlan.TargetIndex,
            cancellationToken);
    }

    public async Task<ExistingExecutionRunChatProjectionPlan> PrepareExistingRunUpdateAsync(
        ExistingExecutionRunPersistencePlan persistencePlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistencePlan);

        var previousChatIndex = await LoadOrBuildChatIndexAsync(
            cancellationToken);
        var targetChatIndex = await CreateRunDetailChatIndexAsync(
            previousChatIndex,
            persistencePlan.PreviousDetail,
            persistencePlan.TargetDetail,
            persistencePlan.TargetIndex,
            cancellationToken);
        return new ExistingExecutionRunChatProjectionPlan(
            previousChatIndex,
            targetChatIndex);
    }

    public async Task PersistExistingRunUpdateAsync(
        ExistingExecutionRunPersistencePlan persistencePlan,
        ExistingExecutionRunChatProjectionPlan chatProjectionPlan,
        ExistingRunDetailCommitOrigin origin,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(persistencePlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.PreviousIndex);
        ArgumentNullException.ThrowIfNull(chatProjectionPlan.TargetIndex);

        var expectedTarget = await CreateRunDetailChatIndexAsync(
            chatProjectionPlan.PreviousIndex,
            persistencePlan.PreviousDetail,
            persistencePlan.TargetDetail,
            persistencePlan.TargetIndex,
            cancellationToken);
        if (jsonStore.RequiresSave(expectedTarget, chatProjectionPlan.TargetIndex))
        {
            var expectedLegacyTarget = await CreateRunDetailChatIndexAsync(
                chatProjectionPlan.PreviousIndex,
                persistencePlan.PreviousDetail,
                persistencePlan.TargetDetail,
                persistencePlan.TargetIndex,
                cancellationToken,
                includeReportingProjection: false);
            if (jsonStore.RequiresSave(
                    expectedLegacyTarget,
                    chatProjectionPlan.TargetIndex))
            {
                throw new InvalidDataException(
                    $"Pending execution-run update '{persistencePlan.TargetDetail.Run.Id:N}' contains an invalid target chat index.");
            }
        }

        var currentChatIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
            layout.ExecutionChatIndexPath,
            cancellationToken);
        if (currentChatIndex is null ||
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.PreviousIndex) &&
            jsonStore.RequiresSave(
                currentChatIndex,
                chatProjectionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{persistencePlan.TargetDetail.Run.Id:N}' found an unexpected chat index.");
        }

        if (origin == ExistingRunDetailCommitOrigin.Prepared &&
            jsonStore.RequiresSave(currentChatIndex, chatProjectionPlan.TargetIndex)) {
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                chatProjectionPlan.TargetIndex,
                cancellationToken);
        } else {
            await jsonStore.WriteJsonIfChangedAsync(
                layout.ExecutionChatIndexPath,
                chatProjectionPlan.TargetIndex,
                cancellationToken);
        }
    }

    private async Task<ExecutionChatIndex> CreateRunDetailChatIndexAsync(
        ExecutionChatIndex currentChatIndex,
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken,
        bool includeReportingProjection = true)
    {
        var runSummaries = currentChatIndex.RunSummaries
            .Where(item => item.ExecutionRunId != detail.Run.Id)
            .Append(includeReportingProjection
                ? WorkspaceChatProjectionBuilder.CreateChatRunSummary(detail)
                : WorkspaceChatProjectionBuilder.CreateLegacyChatRunSummary(
                    detail.Run,
                    detail.ExecutionLog))
            .ToList();
        runSummaries.Sort(CompareReportRuns);
        var reportingProjectionVersion = runSummaries.All(
            static summary =>
                summary.ReportingProjectionVersion >=
                WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion)
            ? WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
            : 0;

        var affectedSessionIds = new HashSet<Guid>();
        if (previousDetail?.Run.ChatSessionId is Guid previousRunSessionId)
        {
            affectedSessionIds.Add(previousRunSessionId);
        }

        if (detail.Run.ChatSessionId is Guid currentRunSessionId)
        {
            affectedSessionIds.Add(currentRunSessionId);
        }

        if (previousDetail?.ChatSession?.Id is Guid previousSessionId)
        {
            affectedSessionIds.Add(previousSessionId);
        }

        if (detail.ChatSession?.Id is Guid currentSessionId)
        {
            affectedSessionIds.Add(currentSessionId);
        }

        var sessionSummaries = currentChatIndex.SessionSummaries
            .Where(item => !affectedSessionIds.Contains(item.Id))
            .ToList();

        foreach (var sessionId in affectedSessionIds)
        {
            var session = detail.ChatSession?.Id == sessionId
                ? detail.ChatSession
                : await jsonStore.ReadJsonAsync<ChatSessionRecord>(
                    layout.SessionPath(sessionId),
                    cancellationToken);
            if (session is null)
            {
                continue;
            }

            var latestRun = await ResolveLatestRunForSessionAsync(
                session,
                runSummaries,
                detail.Run,
                cancellationToken);
            sessionSummaries.Add(WorkspaceChatProjectionBuilder.CreateChatSessionSummary(session, latestRun));
        }

        return new ExecutionChatIndex(
            Version: executionIndex.Version,
            Revision: executionIndex.Revision,
            UpdatedAtUtc: executionIndex.UpdatedAtUtc,
            SessionSummaries: sessionSummaries
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            RunSummaries: runSummaries,
            ReportingProjectionVersion: reportingProjectionVersion);
    }

    public async Task<bool> SaveSessionAsync(
        ChatSessionRecord? previousSession,
        ChatSessionRecord session,
        ExecutionStorageIndex executionIndex,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(executionIndex);

        var currentChatIndex = await LoadOrBuildChatIndexAsync(cancellationToken);
        var affectedSessionIds = new HashSet<Guid> { session.Id };
        if (previousSession is not null)
        {
            affectedSessionIds.Add(previousSession.Id);
        }

        var sessionSummaries = currentChatIndex.SessionSummaries
            .Where(item => !affectedSessionIds.Contains(item.Id))
            .ToList();

        foreach (var sessionId in affectedSessionIds)
        {
            var storedSession = await jsonStore.ReadJsonAsync<ChatSessionRecord>(layout.SessionPath(sessionId), cancellationToken);
            if (storedSession is null)
            {
                continue;
            }

            var latestRun = await ResolveLatestRunForSessionAsync(
                storedSession,
                currentChatIndex.RunSummaries,
                preferredRun: null,
                cancellationToken: cancellationToken);
            sessionSummaries.Add(WorkspaceChatProjectionBuilder.CreateChatSessionSummary(storedSession, latestRun));
        }

        var nextChatIndex = new ExecutionChatIndex(
            Version: executionIndex.Version,
            Revision: executionIndex.Revision,
            UpdatedAtUtc: executionIndex.UpdatedAtUtc,
            SessionSummaries: sessionSummaries
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            RunSummaries: currentChatIndex.RunSummaries,
            ReportingProjectionVersion: currentChatIndex.ReportingProjectionVersion);

        if (currentChatIndex is not null &&
            File.Exists(layout.ExecutionChatIndexPath) &&
            !jsonStore.RequiresSave(currentChatIndex, nextChatIndex))
        {
            return false;
        }

        await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionChatIndexPath, nextChatIndex, cancellationToken);
        return true;
    }

    private async Task<ExecutionChatIndex> LoadOrBuildReportingChatIndexAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var preparation = await InspectExecutionReportIndexAsync(
                cancellationToken);
            preparation = await MaterializeExecutionReportIndexAsync(
                preparation,
                cancellationToken);
            var published = await TryPublishExecutionReportIndexAsync(
                preparation,
                cancellationToken);
            if (published is not null)
            {
                return published;
            }
        }
    }

    private static bool IsStale(
        ExecutionChatIndex chatIndex,
        ExecutionStorageIndex? executionIndex)
    {
        return executionIndex is not null &&
               (!string.Equals(
                    chatIndex.Version,
                    executionIndex.Version,
                    StringComparison.Ordinal) ||
                chatIndex.Revision != executionIndex.Revision ||
                chatIndex.RunSummaries.Count != executionIndex.RunCount);
    }

    private static ExecutionChatIndex PrepareCurrentReportingIndex(
        ExecutionChatIndex chatIndex,
        bool requiresUpgrade)
    {
        var hasCanonicalOrder = HasCanonicalReportOrder(chatIndex.RunSummaries);
        if (!requiresUpgrade && hasCanonicalOrder)
        {
            return chatIndex;
        }

        var runSummaries = hasCanonicalOrder
            ? chatIndex.RunSummaries
            : chatIndex.RunSummaries
                .OrderByDescending(static summary => summary.ActivityAtUtc)
                .ThenByDescending(static summary => summary.CreatedAtUtc)
                .ThenBy(static summary => summary.ExecutionRunId)
                .ToList();
        return chatIndex with
        {
            RunSummaries = runSummaries,
            ReportingProjectionVersion =
                WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
        };
    }

    private async Task<ExecutionChatIndex> UpgradeReportingChatIndexAsync(
        ExecutionChatIndex chatIndex,
        CancellationToken cancellationToken)
    {
        var upgradedSummaries = chatIndex.RunSummaries.ToArray();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, upgradedSummaries.Length),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = ReportingUpgradeReadConcurrency
            },
            async (index, token) =>
            {
                var summary = upgradedSummaries[index];
                if (summary.ReportingProjectionVersion >=
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion)
                {
                    return;
                }

                var detail = await LoadReportingRunDetailAsync(
                    summary.ExecutionRunId,
                    token);
                if (detail is null)
                {
                    throw new InvalidDataException(
                        $"The agent execution reporting projection references missing canonical run '{summary.ExecutionRunId:N}'.");
                }

                upgradedSummaries[index] =
                    WorkspaceChatProjectionBuilder.CreateChatRunSummary(detail);
            });

        Array.Sort(upgradedSummaries, CompareReportRuns);
        return chatIndex with
        {
            RunSummaries = upgradedSummaries,
            ReportingProjectionVersion =
                WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
        };
    }

    private async Task<ExecutionChatIndex> LoadOrBuildChatIndexAsync(CancellationToken cancellationToken)
    {
        var chatIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(layout.ExecutionChatIndexPath, cancellationToken);
        if (chatIndex is not null)
        {
            return chatIndex;
        }

        var rebuilt = await BuildChatIndexAsync(cancellationToken);
        await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionChatIndexPath, rebuilt, cancellationToken);
        return rebuilt;
    }

    private async Task<ExecutionChatIndex> BuildChatIndexAsync(CancellationToken cancellationToken)
    {
        var sessions = await jsonStore.LoadRecordsFromDirectoryAsync<ChatSessionRecord>(layout.ExecutionSessionsRoot, cancellationToken);
        var runs = new List<ExecutionRunRecord>();
        if (Directory.Exists(layout.ExecutionRunsRoot))
        {
            foreach (var runDirectory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot)
                         .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                         .ThenBy(path => path, StringComparer.Ordinal))
            {
                var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(Path.Combine(runDirectory, "run.json"), cancellationToken);
                if (run is null)
                {
                    continue;
                }

                runs.Add(run);
            }
        }

        var index = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(layout.ExecutionIndexPath, cancellationToken);
        var projection = WorkspaceChatProjectionBuilder.Build(sessions, runs, []);
        return new ExecutionChatIndex(
            Version: string.IsNullOrWhiteSpace(index?.Version) ? "1.0" : index.Version,
            Revision: index?.Revision ?? 1L,
            UpdatedAtUtc: index?.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
            SessionSummaries: projection.SessionSummaries,
            RunSummaries: projection.RunSummaries,
            ReportingProjectionVersion: 0);
    }

    private async Task<ExecutionChatIndex> BuildReportingChatIndexAsync(
        ExecutionStorageIndex? executionIndex,
        CancellationToken cancellationToken)
    {
        var sessions = await jsonStore.LoadRecordsFromDirectoryAsync<ChatSessionRecord>(
            layout.ExecutionSessionsRoot,
            cancellationToken);
        var runs = new List<ExecutionRunRecord>();
        var runSummaries = new List<ChatRunSummaryRecord>();
        if (Directory.Exists(layout.ExecutionRunsRoot))
        {
            foreach (var runDirectory in Directory
                         .EnumerateDirectories(layout.ExecutionRunsRoot)
                         .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                         .ThenBy(path => path, StringComparer.Ordinal))
            {
                var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
                    Path.Combine(runDirectory, "run.json"),
                    cancellationToken);
                if (run is null)
                {
                    continue;
                }

                runs.Add(run);
                var detail = await LoadReportingRunDetailAsync(
                    run,
                    cancellationToken);
                runSummaries.Add(
                    WorkspaceChatProjectionBuilder.CreateChatRunSummary(detail));
            }
        }

        executionIndex ??=
            await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
                layout.ExecutionIndexPath,
                cancellationToken);
        var projection = WorkspaceChatProjectionBuilder.Build(
            sessions,
            runs,
            []);
        runSummaries.Sort(CompareReportRuns);
        return new ExecutionChatIndex(
            Version: string.IsNullOrWhiteSpace(executionIndex?.Version)
                ? "1.0"
                : executionIndex.Version,
            Revision: executionIndex?.Revision ?? 1L,
            UpdatedAtUtc: executionIndex?.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
            SessionSummaries: projection.SessionSummaries,
            RunSummaries: runSummaries,
            ReportingProjectionVersion: WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion);
    }

    private async Task<ExecutionRunDetail?> LoadReportingRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        var run = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
            layout.RunPath(executionRunId),
            cancellationToken);
        return run is null
            ? null
            : await LoadReportingRunDetailAsync(run, cancellationToken);
    }

    private async Task<ExecutionRunDetail> LoadReportingRunDetailAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken)
    {
        var executionLogTask =
            jsonStore.LoadRecordsFromDirectoryAsync<ExecutionLogEntry>(
                layout.RunLogsRoot(run.Id),
                cancellationToken);
        var metricsTask =
            jsonStore.LoadRecordsFromDirectoryAsync<AgentRunMetric>(
                layout.RunMetricsRoot(run.Id),
                cancellationToken);
        var usageTask =
            jsonStore.LoadRecordsFromDirectoryAsync<ProviderUsageObservation>(
                layout.RunUsageRoot(run.Id),
                cancellationToken);
        await Task.WhenAll(executionLogTask, metricsTask, usageTask);
        return new ExecutionRunDetail(
            run,
            ChatSession: null,
            await executionLogTask,
            await metricsTask)
        {
            UsageObservations = await usageTask
        };
    }

    private static NormalizedAgentExecutionReportQuery NormalizeReportQuery(
        AgentExecutionReportQuery query)
    {
        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Agent execution report page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > AgentExecutionReportQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Agent execution report page size must be between 1 and {AgentExecutionReportQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Agent execution report page offset is too large.");
        }

        if (query.CreatedFromUtc > query.CreatedToUtc)
        {
            throw new ArgumentException(
                "Agent execution report created-from time cannot be later than created-to time.",
                nameof(query));
        }

        if (query.ActivityFromUtc > query.ActivityToUtc)
        {
            throw new ArgumentException(
                "Agent execution report activity-from time cannot be later than activity-to time.",
                nameof(query));
        }

        if (query.DailyTrendFromUtc > query.ActivityToUtc)
        {
            throw new ArgumentException(
                "Agent execution report daily-trend-from time cannot be later than activity-to time.",
                nameof(query));
        }

        if (query.UnattributedOnly &&
            query.ProjectIds is { Count: > 0 })
        {
            throw new ArgumentException(
                "Agent execution reporting cannot combine project and unattributed filters.",
                nameof(query));
        }

        if (!query.IncludeAggregate && !query.KnownTotalCount.HasValue)
        {
            throw new ArgumentException(
                "A known total count is required when the agent execution report aggregate is omitted.",
                nameof(query));
        }

        if (query.KnownTotalCount is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.KnownTotalCount,
                "The known agent execution report total count cannot be negative.");
        }

        return new NormalizedAgentExecutionReportQuery(
            NormalizeFilterSet(
                query.SourceKinds,
                nameof(query.SourceKinds)),
            NormalizeFilterSet(
                query.SourceIds,
                nameof(query.SourceIds)),
            NormalizeProjectIds(query.ProjectIds),
            query.UnattributedOnly,
            query.CreatedFromUtc,
            query.CreatedToUtc,
            query.ActivityFromUtc,
            query.ActivityToUtc,
            NormalizeEnumFilter(
                query.State,
                query.States,
                "execution state"),
            NormalizeEnumFilter(
                query.Outcome,
                query.Outcomes,
                "run outcome"),
            NormalizeEnumFilter<AgentExecutionReportStatus>(
                null,
                query.Statuses,
                "report status"),
            query.DailyTrendFromUtc,
            query.PageIndex,
            query.PageSize,
            query.PageIndex * query.PageSize,
            query.ExcludeProcessCorrelatedRuns,
            query.ExcludeWorkflowCorrelatedRuns,
            query.ExcludeInvalidCorrelationRuns,
            query.IncludeAggregate,
            query.KnownTotalCount);
    }

    private static HashSet<Guid>? NormalizeProjectIds(
        IReadOnlyList<Guid>? projectIds)
    {
        if (projectIds is null || projectIds.Count == 0)
        {
            return null;
        }

        var normalized = new HashSet<Guid>();
        foreach (var projectId in projectIds)
        {
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Agent execution report project identifiers cannot be empty.",
                    nameof(projectIds));
            }

            normalized.Add(projectId);
        }

        return normalized;
    }

    private static HashSet<string>? NormalizeFilterSet(
        IReadOnlyList<string>? values,
        string parameterName)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                value,
                parameterName);
            normalized.Add(value.Trim());
        }

        return normalized;
    }

    private static HashSet<T>? NormalizeEnumFilter<T>(
        T? value,
        IReadOnlyList<T>? values,
        string filterName)
        where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterName);
        if (!value.HasValue && (values is null || values.Count == 0))
        {
            return null;
        }

        var normalized = new HashSet<T>();
        if (value.HasValue)
        {
            if (!Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"Agent execution report {filterName} filter is invalid.");
            }

            normalized.Add(value.Value);
        }

        foreach (var item in values ?? [])
        {
            if (!Enum.IsDefined(item))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(values),
                    item,
                    $"Agent execution report {filterName} filter is invalid.");
            }

            normalized.Add(item);
        }

        return normalized;
    }

    private static bool Matches(
        ChatRunSummaryRecord run,
        NormalizedAgentExecutionReportQuery query)
    {
        return MatchesWithoutProjectAttribution(run, query) &&
               (!query.ExcludeInvalidCorrelationRuns ||
                run.InvalidCorrelationIdCount == 0) &&
               (query.ProjectIds is null ||
                run.ProjectId.HasValue &&
                query.ProjectIds.Contains(run.ProjectId.Value)) &&
               (!query.UnattributedOnly ||
                run.ProjectAttributionSource ==
                AgentExecutionProjectAttributionSource.None);
    }

    private static bool MatchesWithoutProjectAttribution(
        ChatRunSummaryRecord run,
        NormalizedAgentExecutionReportQuery query)
    {
        return (query.SourceKinds is null ||
                query.SourceKinds.Contains(run.SourceKind)) &&
               (query.SourceIds is null ||
                query.SourceIds.Contains(run.SourceId)) &&
               (!query.CreatedFromUtc.HasValue ||
                run.CreatedAtUtc >= query.CreatedFromUtc.Value) &&
               (!query.CreatedToUtc.HasValue ||
                run.CreatedAtUtc <= query.CreatedToUtc.Value) &&
               (!query.ActivityFromUtc.HasValue ||
                run.ActivityAtUtc >= query.ActivityFromUtc.Value) &&
               (!query.ActivityToUtc.HasValue ||
                run.ActivityAtUtc <= query.ActivityToUtc.Value) &&
               (query.States is null ||
                query.States.Contains(run.State)) &&
               (query.Outcomes is null ||
                run.Outcome.HasValue &&
                query.Outcomes.Contains(run.Outcome.Value)) &&
               (query.Statuses is null ||
                query.Statuses.Contains(
                    AgentExecutionReportStatusPolicy.Resolve(
                        run.State,
                        run.Outcome))) &&
               (!query.ExcludeProcessCorrelatedRuns ||
                !run.CorrelatedProcessRunId.HasValue) &&
               (!query.ExcludeWorkflowCorrelatedRuns ||
                run.CorrelatedWorkflowRunIds.Count == 0);
    }

    private static int CompareReportRuns(
        ChatRunSummaryRecord left,
        ChatRunSummaryRecord right)
    {
        var result = right.ActivityAtUtc.CompareTo(left.ActivityAtUtc);
        if (result != 0)
        {
            return result;
        }

        result = right.CreatedAtUtc.CompareTo(left.CreatedAtUtc);
        return result != 0
            ? result
            : left.ExecutionRunId.CompareTo(right.ExecutionRunId);
    }

    private static bool HasCanonicalReportOrder(
        IReadOnlyList<ChatRunSummaryRecord> runSummaries)
    {
        for (var index = 1; index < runSummaries.Count; index++)
        {
            if (CompareReportRuns(runSummaries[index - 1], runSummaries[index]) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private ExecutionChatIndex CacheReportingIndex(ExecutionChatIndex chatIndex)
    {
        cachedReportingChatIndex = chatIndex;
        cachedReportingIndexStamp = ReadFileStamp(layout.ExecutionChatIndexPath);
        cachedReportingExecutionIndexStamp = ReadFileStamp(layout.ExecutionIndexPath);
        return chatIndex;
    }

    private static ReportingIndexFileStamp? ReadFileStamp(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return null;
        }

        file.Refresh();
        return new ReportingIndexFileStamp(file.Length, file.LastWriteTimeUtc);
    }

    private static void AddDuration(
        ref long totalDurationTicks,
        TimeSpan? duration)
    {
        if (!duration.HasValue || duration.Value.Ticks <= 0)
        {
            return;
        }

        totalDurationTicks = duration.Value.Ticks > long.MaxValue - totalDurationTicks
            ? long.MaxValue
            : totalDurationTicks + duration.Value.Ticks;
    }

    private static void AddDailyCost(
        SortedDictionary<DateOnly, DailyCostAccumulator> dailyCost,
        ChatRunSummaryRecord run,
        DateTimeOffset? dailyTrendFromUtc)
    {
        var trendAtUtc = run.ActivityAtUtc;
        if (dailyTrendFromUtc.HasValue && trendAtUtc < dailyTrendFromUtc.Value)
        {
            return;
        }

        var dayUtc = DateOnly.FromDateTime(trendAtUtc.UtcDateTime);
        if (!dailyCost.TryGetValue(dayUtc, out var accumulator))
        {
            accumulator = new DailyCostAccumulator();
            dailyCost.Add(dayUtc, accumulator);
        }

        accumulator.RunCount++;
        accumulator.KnownCostUsd += run.KnownCostUsd;
        if (run.HasUnknownCost)
        {
            accumulator.UnknownCostRunCount++;
        }

        if (dailyTrendFromUtc.HasValue ||
            dailyCost.Count <= AgentExecutionReportQueryLimits.MaximumDailyTrendDays)
        {
            return;
        }

        using var enumerator = dailyCost.GetEnumerator();
        if (enumerator.MoveNext())
        {
            dailyCost.Remove(enumerator.Current.Key);
        }
    }

    private async Task<ExecutionRunRecord?> ResolveLatestRunForSessionAsync(
        ChatSessionRecord session,
        IReadOnlyList<ChatRunSummaryRecord> runSummaries,
        ExecutionRunRecord? preferredRun,
        CancellationToken cancellationToken)
    {
        if (session.LatestExecutionRunId.HasValue)
        {
            if (preferredRun is not null &&
                session.LatestExecutionRunId.Value == preferredRun.Id &&
                preferredRun.ChatSessionId == session.Id)
            {
                return preferredRun;
            }

            var latestRun = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
                layout.RunPath(session.LatestExecutionRunId.Value),
                cancellationToken);
            if (latestRun?.ChatSessionId == session.Id)
            {
                return latestRun;
            }
        }

        var latestRunId = runSummaries
            .Where(item =>
                item.ChatSessionId == session.Id &&
                item.ExecutionRunId != session.LatestExecutionRunId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (Guid?)item.ExecutionRunId)
            .FirstOrDefault();
        if (!latestRunId.HasValue)
        {
            return null;
        }

        if (preferredRun is not null &&
            latestRunId.Value == preferredRun.Id &&
            preferredRun.ChatSessionId == session.Id)
        {
            return preferredRun;
        }

        var indexedRun = await jsonStore.ReadJsonAsync<ExecutionRunRecord>(
            layout.RunPath(latestRunId.Value),
            cancellationToken);
        return indexedRun?.ChatSessionId == session.Id
            ? indexedRun
            : null;
    }

    private sealed record NormalizedAgentExecutionReportQuery(
        HashSet<string>? SourceKinds,
        HashSet<string>? SourceIds,
        HashSet<Guid>? ProjectIds,
        bool UnattributedOnly,
        DateTimeOffset? CreatedFromUtc,
        DateTimeOffset? CreatedToUtc,
        DateTimeOffset? ActivityFromUtc,
        DateTimeOffset? ActivityToUtc,
        HashSet<ExecutionState>? States,
        HashSet<RunOutcome>? Outcomes,
        HashSet<AgentExecutionReportStatus>? Statuses,
        DateTimeOffset? DailyTrendFromUtc,
        int PageIndex,
        int PageSize,
        int Offset,
        bool ExcludeProcessCorrelatedRuns,
        bool ExcludeWorkflowCorrelatedRuns,
        bool ExcludeInvalidCorrelationRuns,
        bool IncludeAggregate,
        int? KnownTotalCount);

    private sealed class DailyCostAccumulator
    {
        public decimal KnownCostUsd { get; set; }

        public int RunCount { get; set; }

        public int UnknownCostRunCount { get; set; }
    }
}

internal readonly record struct ReportingIndexFileStamp(
    long Length,
    DateTime LastWriteTimeUtc);

internal sealed record ExecutionReportIndexPreparation(
    ReportingIndexFileStamp? ChatIndexStamp,
    ReportingIndexFileStamp? ExecutionIndexStamp,
    ExecutionChatIndex? SourceIndex,
    ExecutionStorageIndex? ExecutionIndex,
    ExecutionChatIndex? TargetIndex,
    bool RequiresWrite)
{
    public bool RequiresMaterialization => TargetIndex is null;

    public static ExecutionReportIndexPreparation Ready(
        ReportingIndexFileStamp? chatIndexStamp,
        ReportingIndexFileStamp? executionIndexStamp,
        ExecutionChatIndex targetIndex,
        bool requiresWrite)
    {
        return new ExecutionReportIndexPreparation(
            chatIndexStamp,
            executionIndexStamp,
            targetIndex,
            ExecutionIndex: null,
            targetIndex,
            requiresWrite);
    }

    public static ExecutionReportIndexPreparation Pending(
        ReportingIndexFileStamp? chatIndexStamp,
        ReportingIndexFileStamp? executionIndexStamp,
        ExecutionChatIndex? sourceIndex,
        ExecutionStorageIndex? executionIndex)
    {
        return new ExecutionReportIndexPreparation(
            chatIndexStamp,
            executionIndexStamp,
            sourceIndex,
            executionIndex,
            TargetIndex: null,
            RequiresWrite: true);
    }

    public ExecutionReportIndexPreparation WithTarget(
        ExecutionChatIndex targetIndex)
    {
        ArgumentNullException.ThrowIfNull(targetIndex);
        return this with
        {
            TargetIndex = targetIndex,
            RequiresWrite = true
        };
    }
}

public sealed partial class FileSandboxWorkspaceStore
{
    public async Task<AgentExecutionReportPage> QueryExecutionReportAsync(
        AgentExecutionReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ExecutionReportIndexPreparation? prepared = null;
        ExecutionChatIndex? chatIndex = null;
        while (chatIndex is null)
        {
            var step = await AdvanceExecutionReportIndexAsync(
                prepared,
                cancellationToken);
            chatIndex = step.Index;
            if (chatIndex is not null)
            {
                break;
            }

            prepared = await chatProjectionStore.MaterializeExecutionReportIndexAsync(
                step.Preparation!,
                cancellationToken);
        }

        return chatProjectionStore.QueryExecutionReport(
            chatIndex,
            query,
            cancellationToken);
    }

    private async Task<(
        ExecutionChatIndex? Index,
        ExecutionReportIndexPreparation? Preparation)> AdvanceExecutionReportIndexAsync(
        ExecutionReportIndexPreparation? prepared,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock =
                await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

            if (prepared is not null)
            {
                var published =
                    await chatProjectionStore.TryPublishExecutionReportIndexAsync(
                        prepared,
                        cancellationToken);
                if (published is not null)
                {
                    return (published, Preparation: null);
                }
            }

            while (true)
            {
                var current =
                    await chatProjectionStore.InspectExecutionReportIndexAsync(
                        cancellationToken);
                if (current.RequiresMaterialization)
                {
                    return (Index: null, current);
                }

                var published =
                    await chatProjectionStore.TryPublishExecutionReportIndexAsync(
                        current,
                        cancellationToken);
                if (published is not null)
                {
                    return (published, Preparation: null);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }
}

internal sealed record ExistingExecutionRunChatProjectionPlan(
    ExecutionChatIndex PreviousIndex,
    ExecutionChatIndex TargetIndex);

internal sealed record GenericNewExecutionRunChatProjectionPlan(
    ExecutionChatIndex PreviousIndex,
    ExecutionChatIndex TargetIndex);
