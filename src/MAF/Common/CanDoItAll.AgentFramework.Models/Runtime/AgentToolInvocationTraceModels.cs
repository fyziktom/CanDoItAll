// Pure evidence data moved from CanDoItAll.AgentFramework.Core (SB09).
// The namespace is intentionally kept as CanDoItAll.AgentFramework.Core to preserve
// serialization identity and avoid using-churn in existing consumers.
namespace CanDoItAll.AgentFramework.Core;

public enum ToolInvocationClassification
{
    Unknown,
    Read,
    Mutation,
    Validation,
    HostedProviderNative,
    LocalMcp,
    HostedMcp
}

public sealed record AgentToolInvocationTrace(
    string ToolName,
    ToolInvocationClassification Classification,
    int Sequence,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool Succeeded,
    string FailureMessage)
{
    public string RuntimeToolProviderKey { get; init; } = string.Empty;

    public string RuntimeToolProviderName { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;

    public string TargetPath { get; init; } = string.Empty;

    public Guid? DirectReceiptExecutionRunId { get; init; }
}
