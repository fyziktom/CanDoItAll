namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Recorded execution state of a canonical task with its actual start and end instants, separate from the planned
/// schedule and from displayed progress. The timestamps must match the state: Unknown and NotStarted have neither,
/// Started has an actual start and no actual end, Completed has both, and Cancelled has an actual end and may have an
/// actual start. An actual end cannot precede the actual start.
/// </summary>
/// <param name="State">Execution state, as a JSON integer (see the execution state schema).</param>
/// <param name="ActualStartedAtUtc">Instant work actually started, or null when it has not started.</param>
/// <param name="ActualEndedAtUtc">Instant work actually ended, or null when it has not ended.</param>
public sealed record ProjectTaskExecutionSnapshot(
    ProjectTaskExecutionState State,
    DateTimeOffset? ActualStartedAtUtc,
    DateTimeOffset? ActualEndedAtUtc)
{
    public static ProjectTaskExecutionSnapshot Unknown { get; } =
        new(ProjectTaskExecutionState.Unknown, null, null);

    public static ProjectTaskExecutionSnapshot NotStarted { get; } =
        new(ProjectTaskExecutionState.NotStarted, null, null);
}

public static class ProjectTaskExecutionStatePolicy
{
    public static void Validate(
        ProjectTaskExecutionState state,
        DateTimeOffset? actualStartedAtUtc,
        DateTimeOffset? actualEndedAtUtc)
    {
        if (!Enum.IsDefined(state))
        {
            throw new InvalidOperationException($"Task execution state '{state}' is not defined.");
        }

        switch (state)
        {
            case ProjectTaskExecutionState.Unknown when actualStartedAtUtc.HasValue || actualEndedAtUtc.HasValue:
                throw new InvalidOperationException("An unknown task execution state cannot contain actual execution timestamps.");
            case ProjectTaskExecutionState.NotStarted when actualStartedAtUtc.HasValue || actualEndedAtUtc.HasValue:
                throw new InvalidOperationException("A task that has not started cannot contain actual execution timestamps.");
            case ProjectTaskExecutionState.Started when !actualStartedAtUtc.HasValue:
                throw new InvalidOperationException("A started task requires an actual start timestamp.");
            case ProjectTaskExecutionState.Started when actualEndedAtUtc.HasValue:
                throw new InvalidOperationException("A started task cannot contain an actual end timestamp.");
            case ProjectTaskExecutionState.Completed when !actualStartedAtUtc.HasValue:
                throw new InvalidOperationException("A completed task requires an actual start timestamp.");
            case ProjectTaskExecutionState.Completed when !actualEndedAtUtc.HasValue:
                throw new InvalidOperationException("A completed task requires an actual end timestamp.");
            case ProjectTaskExecutionState.Cancelled when !actualEndedAtUtc.HasValue:
                throw new InvalidOperationException("A cancelled task requires an actual end timestamp.");
        }

        if (actualStartedAtUtc.HasValue &&
            actualEndedAtUtc.HasValue &&
            actualEndedAtUtc.Value < actualStartedAtUtc.Value)
        {
            throw new InvalidOperationException("A task actual end timestamp cannot precede its actual start timestamp.");
        }
    }

    public static void ValidateTransition(
        ProjectTaskExecutionState current,
        ProjectTaskExecutionState proposed)
    {
        if (!Enum.IsDefined(current))
        {
            throw new ArgumentOutOfRangeException(nameof(current), current, "Current task execution state is not defined.");
        }

        if (!Enum.IsDefined(proposed))
        {
            throw new ArgumentOutOfRangeException(nameof(proposed), proposed, "Proposed task execution state is not defined.");
        }

        if (!CanTransition(current, proposed))
        {
            throw new InvalidOperationException(
                $"Task execution state cannot move from '{current}' to '{proposed}'.");
        }
    }

    public static bool CanTransition(
        ProjectTaskExecutionState current,
        ProjectTaskExecutionState proposed)
    {
        if (!Enum.IsDefined(current) || !Enum.IsDefined(proposed))
        {
            return false;
        }

        if (current == proposed || current == ProjectTaskExecutionState.Unknown)
        {
            return true;
        }

        return current switch
        {
            ProjectTaskExecutionState.NotStarted => proposed is
                ProjectTaskExecutionState.Started or
                ProjectTaskExecutionState.Completed or
                ProjectTaskExecutionState.Cancelled,
            ProjectTaskExecutionState.Started => proposed is
                ProjectTaskExecutionState.Completed or
                ProjectTaskExecutionState.Cancelled,
            ProjectTaskExecutionState.Completed => false,
            ProjectTaskExecutionState.Cancelled => false,
            _ => false
        };
    }

    public static bool AllowsAuthoritativeRepricing(ProjectTaskExecutionState state)
        => state == ProjectTaskExecutionState.NotStarted;

    // Every canvas node carries a status-backed progress hint (a "Draft" task renders as 28 %). For a canonical task
    // the recorded execution state is the authoritative fact on every plan surface: a task that has not started has
    // no progress, a completed task is done, a cancelled task has no trackable progress, and a task without a
    // recorded state keeps the hint.
    public static int ResolveExecutionBackedProgress(ProjectTaskExecutionState state, int progressPercent)
        => state switch
        {
            ProjectTaskExecutionState.NotStarted => 0,
            ProjectTaskExecutionState.Completed => 100,
            ProjectTaskExecutionState.Cancelled => ProjectProgressPolicy.UntrackedPercent,
            _ => progressPercent
        };

    public static ProjectTaskExecutionState ResolveAuthoritativePricingState(
        ProjectTaskExecutionState current,
        ProjectTaskExecutionState proposed)
    {
        ValidateTransition(current, proposed);
        return current == ProjectTaskExecutionState.NotStarted ||
            proposed == ProjectTaskExecutionState.NotStarted
                ? ProjectTaskExecutionState.NotStarted
                : proposed;
    }
}
