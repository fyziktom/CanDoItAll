// Pure evidence data moved from CanDoItAll.AgentFramework.Core (SB09).
// The namespace is intentionally kept as CanDoItAll.AgentFramework.Core to preserve
// serialization identity and avoid using-churn in existing consumers.
namespace CanDoItAll.AgentFramework.Core;

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

public enum AgentContextContributionStatus
{
    Provided,
    Skipped,
    Failed
}

public sealed record AgentContextContributionTrace(
    AgentContextContributorId ContributorId,
    AgentContextContributionStatus Status,
    int GeneratedMessageCount,
    IReadOnlyDictionary<string, string> TraceMetadata,
    string FailureMessage,
    TimeSpan? Elapsed);
