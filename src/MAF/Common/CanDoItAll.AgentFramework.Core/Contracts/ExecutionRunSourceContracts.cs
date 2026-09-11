using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record ExecutionRunSourceKey
{
    public ExecutionRunSourceKey(string sourceKind, string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        SourceKind = sourceKind.Trim();
        SourceId = sourceId.Trim();
    }

    private ExecutionRunSourceKey(string sourceKind, string sourceId, string correlationId, string causationId)
        : this(sourceKind, sourceId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);
        CorrelationId = correlationId;
        CausationId = causationId;
    }

    public static ExecutionRunSourceKey ForBackground(string sourceKind, string sourceId, string correlationId, string causationId)
        => new(sourceKind, sourceId, correlationId, causationId);

    public string? CorrelationId { get; }
    public string? CausationId { get; }
    public bool RequiresBackgroundAdmission => CorrelationId is not null;

    public bool MatchesBackgroundLineage(ExecutionRunRecord run) {
        ArgumentNullException.ThrowIfNull(run);
        return RequiresBackgroundAdmission && string.Equals(SourceKind, run.SourceKind, StringComparison.OrdinalIgnoreCase) &&
            CorrelationId == run.CorrelationId && CausationId == run.CausationId;
    }

    public string SourceKind { get; }

    public string SourceId { get; }

    public bool Matches(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return string.Equals(SourceKind, run.SourceKind, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(SourceId, run.SourceId, StringComparison.OrdinalIgnoreCase) &&
               (!RequiresBackgroundAdmission || MatchesBackgroundLineage(run));
    }
}

public enum ExecutionRunSourceDisposition
{
    Created,
    ReusedCompleted,
    ExistingActive,
    ExistingAdmittedFailure,
    SourceReconciliationRequired
}

public sealed record ExecutionRunSourceReservationResult(
    ExecutionRunSourceDisposition Disposition,
    ExecutionRunRecord Run);

public sealed record ExecutionRunSourceExecutionResult(
    ExecutionRunSourceDisposition Disposition,
    ExecutionRunRecord Run,
    ExecutionRunResult? CreatedExecutionResult);

public interface ISandboxWorkspaceExecutionRunReservationStore
{
    Task<ExecutionRunSourceReservationResult> ReserveBackgroundExecutionRunAsync(
        ExecutionRunSourceKey source, ExecutionRunDetail candidate, AgentToolBackgroundReservation reservation,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The workspace store does not support admitted background reservation.");

    Task<ExecutionRunSourceReservationResult> ReserveExecutionRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunDetail candidate,
        CancellationToken cancellationToken = default);
}
