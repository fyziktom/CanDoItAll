using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed record LlmChatDefinitionCreateScope {
    public const int MaximumIdentityLength = 128;

    public LlmChatDefinitionCreateScope(string producer, string actor, string historyNamespace) {
        Producer = Normalize(producer, nameof(producer));
        Actor = Normalize(actor, nameof(actor));
        HistoryNamespace = Normalize(historyNamespace, nameof(historyNamespace));
    }

    public string Producer { get; }
    public string Actor { get; }
    public string HistoryNamespace { get; }

    private static string Normalize(string value, string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        var normalized = value.Trim();
        if (normalized.Length > MaximumIdentityLength) {
            throw new ArgumentException($"A create scope identity cannot exceed {MaximumIdentityLength} characters.", name);
        }

        return normalized;
    }
}

public readonly record struct LlmChatDefinitionCreateIntentId {
    [JsonConstructor]
    public LlmChatDefinitionCreateIntentId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public sealed record LlmChatDefinitionCreateKey {
    public LlmChatDefinitionCreateKey(LlmChatDefinitionCreateScope scope, LlmChatDefinitionCreateIntentId intentId) {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfEqual(intentId.Value, Guid.Empty);
        Scope = scope;
        IntentId = intentId;
    }

    public LlmChatDefinitionCreateScope Scope { get; }
    public LlmChatDefinitionCreateIntentId IntentId { get; }
}

public sealed record CreateLlmChatDefinitionOnceCommand(
    LlmChatDefinitionCreateKey Key,
    CreateLlmChatDefinitionCommand Definition);

public sealed record LlmChatDefinitionCreateReceipt(
    LlmChatDefinitionCreateKey Key,
    LlmChatDefinitionId DefinitionId,
    LlmChatDefinitionRevisionNumber DefinitionRevision,
    long OriginalConcurrencyToken,
    DateTimeOffset CreatedAtUtc);

public sealed record LlmChatDefinitionCreateResponse(LlmChatDefinitionCreateReceipt Receipt, bool WasReplay);

public interface ILlmChatDefinitionCreateReceiptService {
    Task<Result<LlmChatDefinitionCreateResponse>> CreateOnceAsync(
        CreateLlmChatDefinitionOnceCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatDefinitionCreateReceipt?>> FindReceiptAsync(
        LlmChatDefinitionCreateKey key,
        CancellationToken cancellationToken = default);
}
