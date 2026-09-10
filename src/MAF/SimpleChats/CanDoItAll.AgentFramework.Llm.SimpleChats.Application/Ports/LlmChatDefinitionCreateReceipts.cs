using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

public sealed record LlmChatDefinitionCreateClaim(
    LlmChatDefinitionCreateReceipt Receipt,
    LlmChatDefinitionCreateFingerprint Fingerprint) {
    public const int SemanticVersion = 1;
}

public interface ILlmChatDefinitionCreateReceiptRepository {
    Task<LlmChatDefinitionCreateClaim?> TryGetReceiptAsync(
        LlmChatDefinitionCreateKey key,
        CancellationToken cancellationToken = default);

    Task<bool> TryClaimAsync(
        LlmChatDefinitionCreateClaim claim,
        CancellationToken cancellationToken = default);

    void ForgetAttempt(LlmChatDefinitionId definitionId);
}
