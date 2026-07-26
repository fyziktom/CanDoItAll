using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeChildRunParentQuery
{
    private static readonly string ParentRunKeySnippet = JsonSerializer.Serialize(ProcessRuntimeLaunchVariables.ParentProcessRunId);

    public const int TerminalChildRunPageSize = 250;

    public sealed record ParentStep(Guid RunId, Guid StepInstanceId);
    public sealed record ParentClaim(Guid RunId, Guid StepInstanceId, Guid ClaimToken, string OwnerId);
    public readonly record struct TerminalChildRunCursor(Guid RunId, DateTimeOffset UpdatedAtUtc);
    public sealed record TerminalChildRunCandidate(Guid RunId, DateTimeOffset UpdatedAtUtc)
    {
        public TerminalChildRunCursor Cursor => new(RunId, UpdatedAtUtc);
    }

    public static bool IsStoppedChildStatus(ProcessRuntimeStatus status)
    {
        return ProcessRuntimeTerminalStates.IsChildRunStopped(status);
    }

    public static async Task<ProcessRuntimeStatus?> LoadStoppedChildStatusAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var status = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.RunId == childRunId)
            .Select(state => (ProcessRuntimeStatus?)state.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return status is not null && IsStoppedChildStatus(status.Value)
            ? status
            : null;
    }

    public static async Task<IReadOnlyList<Guid>> LoadActiveParentRunIdsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var childLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.RunId == childRunId &&
                assignment.LaunchVariablesJson.Contains(ParentRunKeySnippet))
            .Select(assignment => assignment.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var parentRunIds = childLaunchVariables
            .Select(TryReadParentRunId)
            .Where(parentRunId => parentRunId.HasValue)
            .Select(parentRunId => parentRunId!.Value)
            .Distinct()
            .ToArray();
        if (parentRunIds.Length == 0)
        {
            return [];
        }

        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                parentRunIds.Contains(state.RunId) &&
                state.Status == ProcessRuntimeStatus.Active)
            .OrderBy(state => state.UpdatedAtUtc)
            .Select(state => state.RunId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<ParentStep>> LoadActiveParentStepsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var childLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.RunId == childRunId &&
                assignment.LaunchVariablesJson.Contains(ParentRunKeySnippet))
            .Select(assignment => assignment.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var requestedParentSteps = childLaunchVariables
            .Select(TryReadParentStep)
            .Where(parentStep => parentStep is not null)
            .Select(parentStep => parentStep!)
            .Distinct()
            .ToArray();
        if (requestedParentSteps.Length == 0)
        {
            return [];
        }

        var parentRunIds = requestedParentSteps
            .Select(parentStep => parentStep.RunId)
            .Distinct()
            .ToArray();
        var parentStepIds = requestedParentSteps
            .Select(parentStep => parentStep.StepInstanceId)
            .Distinct()
            .ToArray();

        var candidates = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                parentRunIds.Contains(state.RunId) &&
                state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, Step = step })
            .Where(item =>
                parentStepIds.Contains(item.Step.StepInstanceId) &&
                item.Step.IsExecutable &&
                item.Step.ActiveClaimToken == null &&
                (item.Step.Status == ProcessRuntimeStepStatus.Waiting ||
                 item.Step.Status == ProcessRuntimeStepStatus.Blocked ||
                 item.Step.Status == ProcessRuntimeStepStatus.Failed))
            .Select(item => new ParentStep(item.RunId, item.Step.StepInstanceId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeSiblingParentSteps = await LoadActiveSiblingParentStepsAsync(
            dbContext,
            childRunId,
            cancellationToken).ConfigureAwait(false);

        return candidates
            .Distinct()
            .Where(parentStep => !activeSiblingParentSteps.Contains(parentStep))
            .OrderBy(parentStep => parentStep.RunId)
            .ThenBy(parentStep => parentStep.StepInstanceId)
            .ToArray();
    }

    public static async Task<IReadOnlyList<Guid>> LoadBlockedParentRunIdsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var requestedParentSteps = await LoadRequestedParentStepsAsync(
            dbContext,
            childRunId,
            cancellationToken).ConfigureAwait(false);
        if (requestedParentSteps.Count == 0)
        {
            return [];
        }

        var parentRunIds = requestedParentSteps
            .Select(parentStep => parentStep.RunId)
            .Distinct()
            .ToArray();
        var parentStepIds = requestedParentSteps
            .Select(parentStep => parentStep.StepInstanceId)
            .Distinct()
            .ToArray();
        var candidates = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                parentRunIds.Contains(state.RunId) &&
                state.Status == ProcessRuntimeStatus.Blocked)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, Step = step })
            .Where(item => parentStepIds.Contains(item.Step.StepInstanceId))
            .Select(item => new ParentStep(item.RunId, item.Step.StepInstanceId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var requested = requestedParentSteps.ToHashSet();
        return candidates
            .Where(requested.Contains)
            .Select(parentStep => parentStep.RunId)
            .Distinct()
            .OrderBy(runId => runId)
            .ToArray();
    }

    public static async Task<IReadOnlyList<ParentClaim>> LoadActiveParentClaimsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var childTerminalAtUtc = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.RunId == childRunId &&
                (state.Status == ProcessRuntimeStatus.Completed ||
                 state.Status == ProcessRuntimeStatus.Failed ||
                 state.Status == ProcessRuntimeStatus.Cancelled ||
                 state.Status == ProcessRuntimeStatus.Blocked))
            .Select(state => (DateTimeOffset?)state.UpdatedAtUtc)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (childTerminalAtUtc is null)
        {
            return [];
        }

        var requestedParentSteps = await LoadRequestedParentStepsAsync(
            dbContext,
            childRunId,
            cancellationToken).ConfigureAwait(false);
        if (requestedParentSteps.Count == 0)
        {
            return [];
        }

        var parentRunIds = requestedParentSteps
            .Select(parentStep => parentStep.RunId)
            .Distinct()
            .ToArray();
        var parentStepIds = requestedParentSteps
            .Select(parentStep => parentStep.StepInstanceId)
            .Distinct()
            .ToArray();

        var candidates = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                parentRunIds.Contains(state.RunId) &&
                state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, Step = step })
            .Where(item =>
                parentStepIds.Contains(item.Step.StepInstanceId) &&
                item.Step.IsExecutable &&
                item.Step.ActiveClaimToken != null &&
                (item.Step.Status == ProcessRuntimeStepStatus.Claimed ||
                 item.Step.Status == ProcessRuntimeStepStatus.Running))
            .Join(
                dbContext.DispatchClaims.AsNoTracking(),
                item => item.RunId,
                claim => claim.RunId,
                (item, claim) => new { item.RunId, item.Step, Claim = claim })
            .Where(item =>
                item.Step.ActiveClaimToken == item.Claim.ClaimToken &&
                item.Claim.CreatedAtUtc < childTerminalAtUtc.Value &&
                (item.Claim.Status == DispatchClaimStatus.Claimed ||
                 item.Claim.Status == DispatchClaimStatus.LeaseRenewed ||
                 item.Claim.Status == DispatchClaimStatus.Reclaimed))
            .Select(item => new ParentClaim(
                item.RunId,
                item.Step.StepInstanceId,
                item.Claim.ClaimToken,
                item.Claim.OwnerId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Distinct()
            .OrderBy(parentClaim => parentClaim.RunId)
            .ThenBy(parentClaim => parentClaim.StepInstanceId)
            .ToArray();
    }

    public static async Task<IReadOnlyList<TerminalChildRunCandidate>> LoadTerminalChildRunsPageAsync(
        ProcessPersistenceDbContext dbContext,
        TerminalChildRunCursor? cursor = null,
        DateTimeOffset? updatedAtOrAfterUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (updatedAtOrAfterUtc is { } cutoff && cutoff.Offset != TimeSpan.Zero)
        {
            updatedAtOrAfterUtc = cutoff.ToUniversalTime();
        }

        var query = dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.Status == ProcessRuntimeStatus.Completed ||
                state.Status == ProcessRuntimeStatus.Failed ||
                state.Status == ProcessRuntimeStatus.Cancelled ||
                state.Status == ProcessRuntimeStatus.Blocked)
            .Where(state => updatedAtOrAfterUtc == null || state.UpdatedAtUtc >= updatedAtOrAfterUtc)
            .Where(state => dbContext.RuntimeStepAssignments
                .AsNoTracking()
                .Any(assignment =>
                    assignment.RunId == state.RunId &&
                    assignment.LaunchVariablesJson.Contains(ParentRunKeySnippet)));
        if (cursor is not null)
        {
            query = query.Where(state =>
                state.UpdatedAtUtc < cursor.Value.UpdatedAtUtc ||
                state.UpdatedAtUtc == cursor.Value.UpdatedAtUtc &&
                state.RunId.CompareTo(cursor.Value.RunId) < 0);
        }

        return await query
            .OrderByDescending(state => state.UpdatedAtUtc)
            .ThenByDescending(state => state.RunId)
            .Select(state => new TerminalChildRunCandidate(state.RunId, state.UpdatedAtUtc))
            .Take(TerminalChildRunPageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ParentStep>> LoadRequestedParentStepsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken)
    {
        var childLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.RunId == childRunId &&
                assignment.LaunchVariablesJson.Contains(ParentRunKeySnippet))
            .Select(assignment => assignment.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return childLaunchVariables
            .Select(TryReadParentStep)
            .Where(parentStep => parentStep is not null)
            .Select(parentStep => parentStep!)
            .Distinct()
            .ToArray();
    }

    private static async Task<HashSet<ParentStep>> LoadActiveSiblingParentStepsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken)
    {
        var activeChildLaunchVariables = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.RunId != childRunId &&
                state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.RuntimeStepAssignments.AsNoTracking(),
                state => state.RunId,
                assignment => assignment.RunId,
                (state, assignment) => assignment.LaunchVariablesJson)
            .Where(launchVariablesJson => launchVariablesJson.Contains(ParentRunKeySnippet))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return activeChildLaunchVariables
            .Select(TryReadParentStep)
            .Where(parentStep => parentStep is not null)
            .Select(parentStep => parentStep!)
            .ToHashSet();
    }

    private static Guid? TryReadParentRunId(string launchVariablesJson)
    {
        return ProcessRuntimeLaunchVariables.TryReadParentRunId(launchVariablesJson, out var parentRunId)
            ? parentRunId.Value
            : null;
    }

    private static ParentStep? TryReadParentStep(string launchVariablesJson)
    {
        return ProcessRuntimeLaunchVariables.TryReadParentStep(launchVariablesJson, out var parentStep)
            ? new ParentStep(parentStep.RunId.Value, parentStep.StepInstanceId.Value)
            : null;
    }
}
