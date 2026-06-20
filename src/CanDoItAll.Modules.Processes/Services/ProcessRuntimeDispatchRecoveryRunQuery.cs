using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeDispatchRecoveryRunQuery
{
    private const int MaxDispatchableRuns = 500;
    private const int MaxDispatchableCandidateRows = MaxDispatchableRuns * 8;
    private const int MaxExpiredClaimRuns = 250;

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

        var dispatchableCandidateRows = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active)
            .Where(state => readyUpdatedAfterUtc == null || state.UpdatedAtUtc >= readyUpdatedAfterUtc)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, state.UpdatedAtUtc, Step = step })
            .Where(item =>
                item.Step.IsExecutable &&
                item.Step.Status == ProcessRuntimeStepStatus.Ready &&
                item.Step.ActiveClaimToken == null)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Step.AttemptNumber)
            .ThenBy(item => item.Step.StepInstanceId)
            .Select(item => new DispatchableCandidate(
                item.RunId,
                item.Step.StepInstanceId,
                item.UpdatedAtUtc))
            .Take(MaxDispatchableCandidateRows)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var schedulablePendingCandidateRows = await LoadSchedulablePendingCandidateRowsAsync(
            dbContext,
            cancellationToken).ConfigureAwait(false);

        var activeChildParentSteps = await LoadActiveChildParentStepsAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var dispatchableRunIds = dispatchableCandidateRows
            .Concat(schedulablePendingCandidateRows)
            .GroupBy(candidate => candidate.RunId)
            .OrderByDescending(group => group.Max(candidate => candidate.UpdatedAtUtc))
            .Where(group => group.Any(candidate => !activeChildParentSteps.Contains((candidate.RunId, candidate.StepInstanceId))))
            .Select(group => group.Key)
            .Take(MaxDispatchableRuns)
            .ToArray();

        var expiredClaimRunIds = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.DispatchClaims.AsNoTracking(),
                state => state.RunId,
                claim => claim.RunId,
                (state, claim) => new { state.RunId, state.UpdatedAtUtc, Claim = claim })
            .Where(item =>
                item.Claim.ExpiresAtUtc <= nowUtc &&
                (item.Claim.Status == DispatchClaimStatus.Claimed ||
                 item.Claim.Status == DispatchClaimStatus.LeaseRenewed ||
                 item.Claim.Status == DispatchClaimStatus.Reclaimed))
            .GroupBy(item => item.RunId)
            .Select(group => new
            {
                RunId = group.Key,
                UpdatedAtUtc = group.Max(item => item.UpdatedAtUtc)
            })
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => item.RunId)
            .Take(MaxExpiredClaimRuns)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var runIds = new List<Guid>(dispatchableRunIds.Length + expiredClaimRunIds.Length);
        var seen = new HashSet<Guid>();
        AddUnique(runIds, seen, dispatchableRunIds);
        AddUnique(runIds, seen, expiredClaimRunIds);
        return runIds;
    }

    private static async Task<IReadOnlyList<DispatchableCandidate>> LoadSchedulablePendingCandidateRowsAsync(
        ProcessPersistenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pendingCandidateRows = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active || state.Status == ProcessRuntimeStatus.Created)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, state.UpdatedAtUtc, Step = step })
            .Where(item =>
                item.Step.IsExecutable &&
                item.Step.Status == ProcessRuntimeStepStatus.Pending &&
                item.Step.ActiveClaimToken == null)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Step.AttemptNumber)
            .ThenBy(item => item.Step.StepInstanceId)
            .Select(item => new PendingStepCandidate(
                item.RunId,
                item.Step.StepInstanceId,
                item.UpdatedAtUtc,
                item.Step.DependencyStepIds,
                item.Step.RequiredArtifactSlotIds))
            .Take(MaxDispatchableCandidateRows)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (pendingCandidateRows.Length == 0)
        {
            return [];
        }

        var candidateRunIds = pendingCandidateRows
            .Select(candidate => candidate.RunId)
            .Distinct()
            .ToArray();
        var stepRows = await dbContext.RuntimeSteps
            .AsNoTracking()
            .Where(step => candidateRunIds.Contains(step.RunId))
            .Select(step => new RuntimeStepStatusRow(
                step.RunId,
                step.StepInstanceId,
                step.Status))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var availableSlots = await dbContext.AvailableArtifactSlots
            .AsNoTracking()
            .Where(slot => candidateRunIds.Contains(slot.RunId))
            .Select(slot => new AvailableArtifactSlotRow(slot.RunId, slot.SlotId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var stepsByRun = stepRows
            .GroupBy(step => step.RunId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(step => step.StepInstanceId, step => step.Status));
        var slotsByRun = availableSlots
            .GroupBy(slot => slot.RunId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(slot => slot.SlotId).ToHashSet());
        var candidates = new List<DispatchableCandidate>();
        foreach (var pendingCandidate in pendingCandidateRows)
        {
            if (!stepsByRun.TryGetValue(pendingCandidate.RunId, out var stepStatuses))
            {
                continue;
            }

            slotsByRun.TryGetValue(pendingCandidate.RunId, out var runAvailableSlots);
            runAvailableSlots ??= [];
            if (DependenciesSatisfied(pendingCandidate.DependencyStepIds, stepStatuses) &&
                RequiredArtifactsAvailable(pendingCandidate.RequiredArtifactSlotIds, runAvailableSlots))
            {
                candidates.Add(new DispatchableCandidate(
                    pendingCandidate.RunId,
                    pendingCandidate.StepInstanceId,
                    pendingCandidate.UpdatedAtUtc));
            }
        }

        return candidates;
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

    private sealed record DispatchableCandidate(
        Guid RunId,
        Guid StepInstanceId,
        DateTimeOffset UpdatedAtUtc);

    private sealed record PendingStepCandidate(
        Guid RunId,
        Guid StepInstanceId,
        DateTimeOffset UpdatedAtUtc,
        string DependencyStepIds,
        string RequiredArtifactSlotIds);

    private sealed record RuntimeStepStatusRow(
        Guid RunId,
        Guid StepInstanceId,
        ProcessRuntimeStepStatus Status);

    private sealed record AvailableArtifactSlotRow(
        Guid RunId,
        Guid SlotId);

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
