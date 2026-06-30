using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentContextContributor
{
    AgentContextContributorDescriptor Descriptor { get; }

    ValueTask<AgentContextContributionResult> ContributeAsync(
        AgentContextContributionRequest request,
        CancellationToken cancellationToken = default);
}

public readonly record struct AgentContextContributorId
{
    public AgentContextContributorId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record AgentContextContributorDescriptor(
    AgentContextContributorId Id,
    string DisplayName,
    int Order,
    bool Enabled = true);

public enum AgentContextExecutionMode
{
    InteractiveChat,
    GovernedProcessAutomation,
    AutoApprovedNonInteractive,
    A2AEndpoint
}

public enum AgentContextMessageRole
{
    System,
    User,
    Assistant
}

public enum AgentContextContributionStatus
{
    Provided,
    Skipped,
    Failed
}

public sealed record AgentContextContributionPolicy(
    AgentContextExecutionMode ExecutionMode,
    bool SuppressApprovalRequirements,
    WorkspaceScopeDescriptor WorkspaceScope);

public sealed record AgentContextRequestMessage(
    AgentContextMessageRole Role,
    string Text);

public sealed record AgentContextMessage(
    AgentContextMessageRole Role,
    string Text);

public sealed record AgentContextContributionRequest(
    AgentDefinition Agent,
    ProviderProfile Provider,
    IReadOnlyList<AgentContextRequestMessage> RequestMessages,
    AgentContextContributionPolicy Policy);

public sealed record AgentContextContributionResult(
    AgentContextContributionStatus Status,
    IReadOnlyList<AgentContextMessage> Messages,
    IReadOnlyDictionary<string, string> TraceMetadata,
    string FailureMessage)
{
    public static AgentContextContributionResult Provided(
        IReadOnlyList<AgentContextMessage> messages,
        IReadOnlyDictionary<string, string>? traceMetadata = null)
        => new(
            AgentContextContributionStatus.Provided,
            messages,
            NormalizeTraceMetadata(traceMetadata),
            string.Empty);

    public static AgentContextContributionResult Skipped(
        IReadOnlyDictionary<string, string>? traceMetadata = null)
        => new(
            AgentContextContributionStatus.Skipped,
            [],
            NormalizeTraceMetadata(traceMetadata),
            string.Empty);

    public static AgentContextContributionResult Failed(
        string failureMessage,
        IReadOnlyDictionary<string, string>? traceMetadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
        return new AgentContextContributionResult(
            AgentContextContributionStatus.Failed,
            [],
            NormalizeTraceMetadata(traceMetadata),
            failureMessage.Trim());
    }

    private static IReadOnlyDictionary<string, string> NormalizeTraceMetadata(
        IReadOnlyDictionary<string, string>? traceMetadata)
        => traceMetadata is null || traceMetadata.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : traceMetadata
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(
                    item => item.Key.Trim(),
                    item => item.Value?.Trim() ?? string.Empty,
                    StringComparer.Ordinal);
}

public sealed record AgentContextContributionTrace(
    AgentContextContributorId ContributorId,
    AgentContextContributionStatus Status,
    int GeneratedMessageCount,
    IReadOnlyDictionary<string, string> TraceMetadata,
    string FailureMessage,
    TimeSpan? Elapsed);

public interface IAgentContextContributionTraceSink
{
    void Record(AgentContextContributionTrace trace);
}

public sealed class AgentContextContributionTraceCollector : IAgentContextContributionTraceSink
{
    private readonly object gate = new();
    private readonly List<AgentContextContributionTrace> traces = [];

    public void Record(AgentContextContributionTrace trace)
    {
        lock (gate)
        {
            traces.Add(trace);
        }
    }

    public IReadOnlyList<AgentContextContributionTrace> Snapshot()
    {
        lock (gate)
        {
            return traces.ToList();
        }
    }
}

public sealed class AgentContextContributionException : InvalidOperationException
{
    public AgentContextContributionException(
        AgentContextContributorId contributorId,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ContributorId = contributorId;
    }

    public AgentContextContributorId ContributorId { get; }
}
