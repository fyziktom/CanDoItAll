using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed record LlmChatPreparedDefinitionCreate {
    internal LlmChatPreparedDefinitionCreate(
        CreateLlmChatDefinitionCommand definition,
        LlmChatDefinitionCreateFingerprint fingerprint) {
        Definition = definition;
        Fingerprint = fingerprint;
    }

    public int SemanticVersion => LlmChatDefinitionCreateClaim.SemanticVersion;
    public CreateLlmChatDefinitionCommand Definition { get; }
    public LlmChatDefinitionCreateFingerprint Fingerprint { get; }

    public override string ToString()
        => $"Simple Chat definition create semantic-v{SemanticVersion} {Fingerprint.Value}";
}

public static class LlmChatDefinitionCreatePreparation {
    public static LlmChatPreparedDefinitionCreate Prepare(CreateLlmChatDefinitionCommand command) {
        var normalized = LlmChatDefinitionCreateSemantics.Snapshot(command);
        normalized = normalized with { Tags = normalized.Tags!.ToImmutableArray() };
        return new(normalized, LlmChatDefinitionCreateSemantics.Fingerprint(normalized));
    }
}
