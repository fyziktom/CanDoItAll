using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentContextContributor
{
    AgentContextContributorDescriptor Descriptor { get; }

    ValueTask<AgentContextContributionResult> ContributeAsync(
        AgentContextContributionRequest request,
        CancellationToken cancellationToken = default);
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

public sealed record AgentContextContributionPolicy(
    AgentContextExecutionMode ExecutionMode,
    bool SuppressApprovalRequirements,
    WorkspaceScopeDescriptor WorkspaceScope);

public sealed record AgentContextRequestMessage(
    AgentContextMessageRole Role,
    string Text);

public sealed record AgentContextRequestMessageTextReplacement(
    int MessageIndex,
    string Text);

public sealed record AgentContextRequestMessageTransformation(
    IReadOnlyList<AgentContextRequestMessageTextReplacement> TextReplacements);

public sealed record AgentContextMessage(
    AgentContextMessageRole Role,
    string Text);

public sealed record AgentContextContributionRequest(
    AgentDefinition Agent,
    ProviderProfile Provider,
    IReadOnlyList<AgentContextRequestMessage> RequestMessages,
    AgentContextContributionPolicy Policy)
{
    public AgentRuntimeContextIntent ContextIntent { get; init; } = AgentRuntimeContextIntent.Empty;
}

public sealed record AgentContextContributionResult(
    AgentContextContributionStatus Status,
    IReadOnlyList<AgentContextMessage> Messages,
    IReadOnlyDictionary<string, string> TraceMetadata,
    string FailureMessage)
{
    public AgentContextRequestMessageTransformation? RequestMessageTransformation { get; init; }

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

    public AgentContextContributionResult WithRequestMessageTextReplacement(
        int messageIndex,
        string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(messageIndex);
        ArgumentNullException.ThrowIfNull(text);
        return this with
        {
            RequestMessageTransformation = new AgentContextRequestMessageTransformation(
                [new AgentContextRequestMessageTextReplacement(messageIndex, text)])
        };
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
