using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public interface IProcessExecutionObservationReader
{
    ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessExecutionObservationQuery(
    IReadOnlyList<ProcessRunId> RunIds,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TakePerRun);

public sealed record ProcessExecutionObservation(
    Guid ExecutionRunId,
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    Guid AgentId,
    string AgentName,
    string ProviderName,
    string Model,
    string State,
    string Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string InputSummary,
    string ResultSummary,
    IReadOnlyList<ProcessExecutionActivityObservation> RecentActivities,
    IReadOnlyList<ProcessExecutionToolObservation> RecentTools,
    IReadOnlyList<ProcessExecutionArtifactObservation> Artifacts,
    string LastError);

public sealed record ProcessExecutionActivityObservation(
    DateTimeOffset CreatedAtUtc,
    string State,
    string Phase,
    string Message);

public sealed record ProcessExecutionToolObservation(
    string ToolName,
    string RuntimeToolProviderKey,
    string RequestSummary,
    string ExitSummary,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record ProcessExecutionArtifactObservation(
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string Summary,
    DateTimeOffset CreatedAtUtc);
