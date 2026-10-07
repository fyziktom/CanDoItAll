namespace CanDoItAll.AgentFramework.Voice;

public enum AgentVoiceFailureKind {
    InvalidInput,
    Disabled,
    ProviderUnavailable,
    AccessDenied,
    CapabilityUnavailable
}

public sealed class AgentVoiceException(AgentVoiceFailureKind kind, string message)
    : InvalidOperationException(message) {
    public AgentVoiceFailureKind Kind { get; } = kind;
}
