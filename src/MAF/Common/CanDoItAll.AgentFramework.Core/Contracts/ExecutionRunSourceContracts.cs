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

    public string SourceKind { get; }

    public string SourceId { get; }

    public bool Matches(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return string.Equals(SourceKind, run.SourceKind, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(SourceId, run.SourceId, StringComparison.OrdinalIgnoreCase);
    }
}

public enum ExecutionRunSourceDisposition
{
    Created,
    ReusedCompleted,
    ExistingActive
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
    Task<ExecutionRunSourceReservationResult> ReserveExecutionRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunDetail candidate,
        CancellationToken cancellationToken = default);
}
