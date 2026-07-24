namespace CanDoItAll.Modules.Workbench;

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
