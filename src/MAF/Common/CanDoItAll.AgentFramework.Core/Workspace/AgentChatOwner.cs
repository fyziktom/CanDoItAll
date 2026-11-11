namespace CanDoItAll.AgentFramework.Core;

public readonly record struct AgentChatOwnerId {
    public AgentChatOwnerId(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) {
            throw new ArgumentException("An Agent chat owner requires a lowercase SHA-256 fingerprint.", nameof(value));
        }
        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);
}

public interface IAgentChatOwnerProvider {
    event EventHandler? Changed;

    ValueTask<AgentChatOwnerId> GetOwnerAsync(CancellationToken cancellationToken = default);
}
