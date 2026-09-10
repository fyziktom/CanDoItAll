namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

public readonly record struct LlmChatDefinitionCreateFingerprint {
    public LlmChatDefinitionCreateFingerprint(string value) {
        Value = LlmChatFingerprintValue.Normalize(value, nameof(value));
    }

    public string Value { get; }
}
