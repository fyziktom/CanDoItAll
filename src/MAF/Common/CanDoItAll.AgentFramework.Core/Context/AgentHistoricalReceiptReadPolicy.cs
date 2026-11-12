namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentHistoricalReceiptContextChangedException : InvalidOperationException {
    public AgentHistoricalReceiptContextChangedException() : base("The current historical receipt read context changed.") { }
}

public enum AgentHistoricalReceiptReadPolicy {
    Allowed,
    Denied,
    Unsupported,
    ContextChanged
}

public interface IAgentHistoricalReceiptSourceAuthorityProvider {
    string SourceKind { get; }
    ValueTask<AgentExecutionSourceAuthorityDecision> ResolveHistoricalReadAsync(AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default);
}