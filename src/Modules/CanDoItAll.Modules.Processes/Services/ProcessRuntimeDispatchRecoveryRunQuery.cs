using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeDispatchRecoveryRunQuery
{
    internal const int RecoverableRunPageSize = 250;
    internal const int BlockedRunPageSize = 250;

    private static readonly string ParentRunKeySnippet = JsonSerializer.Serialize(ProcessRuntimeLaunchVariables.ParentProcessRunId);
    private static readonly string ParentStepKeySnippet = JsonSerializer.Serialize(ProcessRuntimeLaunchVariables.ParentProcessStepId);

    public static async Task<IReadOnlyList<Guid>> LoadRecoverableRunIdsAsync(
        ProcessPersistenceDbContext dbContext,
        DateTimeOffset nowUtc,
        DateTimeOffset? readyUpdatedAfterUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (nowUtc.Offset != TimeSpan.Zero)
        {
            nowUtc = nowUtc.ToUniversalTime();
        }

        if (readyUpdatedAfterUtc is { } cutoff && cutoff.Offset != TimeSpan.Zero)
        {
            readyUpdatedAfterUtc = cutoff.ToUniversalTime();
        }

        var activeChildParentSteps = await LoadActiveChildParentStepsAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var dispatchableRunIds = new List<Guid>();
        var expiredClaimRunIds = new List<Guid>();
        RecoverableRunCursor? cursor = null;
        while (true)
        {
            var runtimeStates = await LoadRecoverableRuntimeStatePageAsync(
                dbContext,
                cursor,
                cancellationToken).ConfigureAwait(false);
            if (runtimeStates.Count == 0)
            {
                break;
            }

            var candidateRunIds = runtimeStates
                .Select(state => state.RunId)
                .ToArray();
            var runtimeSteps = await dbContext.RuntimeSteps
                .AsNoTracking()
                .Where(step => candidateRunIds.Contains(step.RunId))
                .Select(step => new RecoveryRuntimeStepCandidate(
                    step.RunId,
                    step.StepInstanceId,
                    step.Status,
                    step.IsExecutable,
                    step.ActiveClaimToken,
                    step.DependencyStepIds,
                    step.RequiredArtifactSlotIds))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            var availableSlots = await dbContext.AvailableArtifactSlots
                .AsNoTracking()
                .Where(slot => candidateRunIds.Contains(slot.RunId))
                .Select(slot => new AvailableArtifactSlotRow(slot.RunId, slot.SlotId))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            var expiredClaimRunIdSet = await dbContext.DispatchClaims
                .AsNoTracking()
                .Where(claim =>
                    candidateRunIds.Contains(claim.RunId) &&
                    claim.ExpiresAtUtc <= nowUtc &&
                    (claim.Status == DispatchClaimStatus.Claimed ||
                     claim.Status == DispatchClaimStatus.LeaseRenewed ||
                     claim.Status == DispatchClaimStatus.Reclaimed))
                .Select(claim => claim.RunId)
                .Distinct()
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);

            var stepsByRun = runtimeSteps
                .GroupBy(step => step.RunId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var slotsByRun = availableSlots
                .GroupBy(slot => slot.RunId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(slot => slot.SlotId).ToHashSet());
            foreach (var runtimeState in runtimeStates)
            {
                if (stepsByRun.TryGetValue(runtimeState.RunId, out var runSteps))
                {
                    slotsByRun.TryGetValue(runtimeState.RunId, out var runAvailableSlots);
                    if (HasDispatchableStep(
                        runtimeState,
                        runSteps,
                        runAvailableSlots ?? [],
                        activeChildParentSteps,
                        readyUpdatedAfterUtc))
                    {
                        dispatchableRunIds.Add(runtimeState.RunId);
                    }
                }

                if (runtimeState.Status == ProcessRuntimeStatus.Active &&
                    expiredClaimRunIdSet.Contains(runtimeState.RunId))
                {
                    expiredClaimRunIds.Add(runtimeState.RunId);
                }
            }

            if (runtimeStates.Count < RecoverableRunPageSize)
            {
                break;
            }

            cursor = runtimeStates[^1].Cursor;
        }

        var runIds = new List<Guid>(dispatchableRunIds.Count + expiredClaimRunIds.Count);
        var seen = new HashSet<Guid>();
        AddUnique(runIds, seen, dispatchableRunIds);
        AddUnique(runIds, seen, expiredClaimRunIds);
        return runIds;
    }

    public static async Task<IReadOnlyList<BlockedRunRecoveryCandidate>> LoadBlockedRunsPageAsync(
        ProcessPersistenceDbContext dbContext,
        BlockedRunRecoveryCursor? cursor = null,
        DateTimeOffset? updatedAtOrAfterUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (updatedAtOrAfterUtc is { } cutoff && cutoff.Offset != TimeSpan.Zero)
        {
            updatedAtOrAfterUtc = cutoff.ToUniversalTime();
        }

        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Blocked)
            .Where(state => updatedAtOrAfterUtc == null || state.UpdatedAtUtc >= updatedAtOrAfterUtc)
            .Where(state =>
                cursor == null ||
                state.UpdatedAtUtc < cursor.Value.UpdatedAtUtc ||
                state.UpdatedAtUtc == cursor.Value.UpdatedAtUtc &&
                state.RunId.CompareTo(cursor.Value.RunId) < 0)
            .OrderByDescending(state => state.UpdatedAtUtc)
            .ThenByDescending(state => state.RunId)
            .Select(state => new BlockedRunRecoveryCandidate(
                state.RunId,
                state.UpdatedAtUtc))
            .Take(BlockedRunPageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<RecoveryRuntimeStateCandidate>> LoadRecoverableRuntimeStatePageAsync(
        ProcessPersistenceDbContext dbContext,
        RecoverableRunCursor? cursor,
        CancellationToken cancellationToken)
    {
        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active || state.Status == ProcessRuntimeStatus.Created)
            .Where(state =>
                cursor == null ||
                state.UpdatedAtUtc < cursor.Value.UpdatedAtUtc ||
                state.UpdatedAtUtc == cursor.Value.UpdatedAtUtc &&
                state.RunId.CompareTo(cursor.Value.RunId) < 0)
            .OrderByDescending(state => state.UpdatedAtUtc)
            .ThenByDescending(state => state.RunId)
            .Select(state => new RecoveryRuntimeStateCandidate(
                state.RunId,
                state.Status,
                state.UpdatedAtUtc))
            .Take(RecoverableRunPageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool HasDispatchableStep(
        RecoveryRuntimeStateCandidate runtimeState,
        IReadOnlyList<RecoveryRuntimeStepCandidate> runtimeSteps,
        IReadOnlySet<Guid> availableSlots,
        IReadOnlySet<(Guid RunId, Guid StepInstanceId)> activeChildParentSteps,
        DateTimeOffset? readyUpdatedAfterUtc)
    {
        var stepStatuses = runtimeSteps.ToDictionary(step => step.StepInstanceId, step => step.Status);
        foreach (var runtimeStep in runtimeSteps)
        {
            if (!runtimeStep.IsExecutable ||
                runtimeStep.ActiveClaimToken is not null ||
                activeChildParentSteps.Contains((runtimeStep.RunId, runtimeStep.StepInstanceId)))
            {
                continue;
            }

            if (runtimeStep.Status == ProcessRuntimeStepStatus.Ready &&
                runtimeState.Status == ProcessRuntimeStatus.Active &&
                (readyUpdatedAfterUtc is null || runtimeState.UpdatedAtUtc >= readyUpdatedAfterUtc))
            {
                return true;
            }

            if (runtimeStep.Status == ProcessRuntimeStepStatus.Pending &&
                DependenciesSatisfied(runtimeStep.DependencyStepIds, stepStatuses) &&
                RequiredArtifactsAvailable(runtimeStep.RequiredArtifactSlotIds, availableSlots))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<HashSet<(Guid RunId, Guid StepInstanceId)>> LoadActiveChildParentStepsAsync(
        ProcessPersistenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var activeChildLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Join(
                dbContext.RuntimeStates.AsNoTracking(),
                assignment => assignment.RunId,
                state => state.RunId,
                (assignment, state) => new { assignment.LaunchVariablesJson, state.Status })
            .Where(item =>
                item.Status != ProcessRuntimeStatus.Completed &&
                item.Status != ProcessRuntimeStatus.Failed &&
                item.Status != ProcessRuntimeStatus.Cancelled &&
                item.Status != ProcessRuntimeStatus.Blocked &&
                item.LaunchVariablesJson.Contains(ParentRunKeySnippet) &&
                item.LaunchVariablesJson.Contains(ParentStepKeySnippet))
            .Select(item => item.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var parentSteps = new HashSet<(Guid RunId, Guid StepInstanceId)>();
        foreach (var launchVariablesJson in activeChildLaunchVariables)
        {
            if (!TryReadParentStep(launchVariablesJson, out var runId, out var stepInstanceId))
            {
                continue;
            }

            parentSteps.Add((runId, stepInstanceId));
        }

        return parentSteps;
    }

    private static bool TryReadParentStep(
        string launchVariablesJson,
        out Guid runId,
        out Guid stepInstanceId)
    {
        runId = Guid.Empty;
        stepInstanceId = Guid.Empty;
        if (!ProcessRuntimeLaunchVariables.TryReadParentStep(launchVariablesJson, out var parentStep))
        {
            return false;
        }

        runId = parentStep.RunId.Value;
        stepInstanceId = parentStep.StepInstanceId.Value;
        return true;
    }

    private static bool DependenciesSatisfied(
        string dependencyStepIds,
        IReadOnlyDictionary<Guid, ProcessRuntimeStepStatus> stepStatuses)
    {
        if (!TrySplitGuids(dependencyStepIds, out var dependencyIds))
        {
            return false;
        }

        foreach (var dependencyId in dependencyIds)
        {
            if (!stepStatuses.TryGetValue(dependencyId, out var dependencyStatus) ||
                !ProcessRuntimeTerminalStates.IsStepTerminal(dependencyStatus) ||
                dependencyStatus is ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RequiredArtifactsAvailable(
        string requiredArtifactSlotIds,
        IReadOnlySet<Guid> availableSlots)
    {
        if (!TrySplitGuids(requiredArtifactSlotIds, out var slotIds))
        {
            return false;
        }

        foreach (var slotId in slotIds)
        {
            if (!availableSlots.Contains(slotId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySplitGuids(
        string value,
        out IReadOnlyList<Guid> guids)
    {
        guids = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parsedGuids = new List<Guid>();
        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var guid) || guid == Guid.Empty)
            {
                return false;
            }

            parsedGuids.Add(guid);
        }

        guids = parsedGuids;
        return true;
    }

    private sealed record RecoveryRuntimeStateCandidate(
        Guid RunId,
        ProcessRuntimeStatus Status,
        DateTimeOffset UpdatedAtUtc)
    {
        public RecoverableRunCursor Cursor => new(UpdatedAtUtc, RunId);
    }

    private sealed record RecoveryRuntimeStepCandidate(
        Guid RunId,
        Guid StepInstanceId,
        ProcessRuntimeStepStatus Status,
        bool IsExecutable,
        Guid? ActiveClaimToken,
        string DependencyStepIds,
        string RequiredArtifactSlotIds);

    private sealed record AvailableArtifactSlotRow(
        Guid RunId,
        Guid SlotId);

    private readonly record struct RecoverableRunCursor(
        DateTimeOffset UpdatedAtUtc,
        Guid RunId);

    internal readonly record struct BlockedRunRecoveryCandidate(
        Guid RunId,
        DateTimeOffset UpdatedAtUtc)
    {
        public BlockedRunRecoveryCursor Cursor => new(UpdatedAtUtc, RunId);
    }

    internal readonly record struct BlockedRunRecoveryCursor(
        DateTimeOffset UpdatedAtUtc,
        Guid RunId);

    private static void AddUnique(
        List<Guid> target,
        HashSet<Guid> seen,
        IEnumerable<Guid> runIds)
    {
        foreach (var runId in runIds)
        {
            if (seen.Add(runId))
            {
                target.Add(runId);
            }
        }
    }
}
