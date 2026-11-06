namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentCatalogConcurrencyException : InvalidOperationException
{
    public AgentCatalogConcurrencyException(
        Guid agentId,
        DateTimeOffset? expectedUpdatedAtUtc,
        DateTimeOffset? actualUpdatedAtUtc)
        : base(BuildMessage(agentId, expectedUpdatedAtUtc, actualUpdatedAtUtc))
    {
        AgentId = agentId;
        ExpectedUpdatedAtUtc = expectedUpdatedAtUtc;
        ActualUpdatedAtUtc = actualUpdatedAtUtc;
    }

    public Guid AgentId { get; }

    public DateTimeOffset? ExpectedUpdatedAtUtc { get; }

    public DateTimeOffset? ActualUpdatedAtUtc { get; }

    private static string BuildMessage(
        Guid agentId,
        DateTimeOffset? expectedUpdatedAtUtc,
        DateTimeOffset? actualUpdatedAtUtc)
    {
        var actual = actualUpdatedAtUtc?.ToString("O") ?? "missing";
        if (expectedUpdatedAtUtc is null) {
            return $"Agent '{agentId:D}' requires its original update timestamp before replacement or deletion. Read and review the agent first.";
        }
        return $"Agent '{agentId:D}' changed after it was read. Expected update timestamp '{expectedUpdatedAtUtc:O}', actual '{actual}'. Reload the agent before applying another patch.";
    }
}
