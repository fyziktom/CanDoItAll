using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;

namespace CanDoItAll.Modules.LlmChats.Ports;

public interface ILlmChatDefinitionRepository
{
    Task<LlmChatDefinition?> TryGetAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default);

    Task<LlmChatDefinitionRevision?> TryGetRevisionAsync(
        LlmChatDefinitionId id,
        LlmChatDefinitionRevisionNumber revision,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmChatDefinition>> ListAsync(
        int take,
        LlmChatDefinitionStatus? status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmChatDefinition>> ListPageAsync(
        int take,
        int offset,
        LlmChatDefinitionStatus? status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListTagsAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default);

    Task ReplaceTagsAsync(
        LlmChatDefinitionId id,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        LlmChatDefinition definition,
        LlmChatDefinitionRevision revision,
        CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        LlmChatDefinition definition,
        long expectedConcurrencyToken,
        LlmChatDefinitionRevision? appendedRevision,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatConversationRepository
{
    Task<LlmChatConversation?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmChatConversation>> ListAsync(
        int take,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmChatConversation>> ListPageAsync(
        int take,
        int offset,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default);

    Task CreateAsync(LlmChatConversation conversation, CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        LlmChatConversation conversation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatOperationRepository
{
    Task<LlmChatOperation?> TryGetAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperationAdmission> AdmitAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation?> TryClaimDispatchAsync(
        LlmChatOperationId id,
        LlmChatRequestFingerprint requestFingerprint,
        CancellationToken cancellationToken = default);

    Task<bool> TryReplaceAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default);
}

public sealed record LlmChatOperationAdmission(
    LlmChatOperation Operation,
    bool Created);

public interface ILlmChatInvocationRecordRepository
{
    Task AppendAsync(LlmChatInvocationRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmChatInvocationRecord>> ListAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatOperationEvidenceSink
{
    Task<LlmChatOperation> MarkTurnAdmittedAsync(
        LlmChatOperationId operationId,
        DateTimeOffset admittedAtUtc,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation> MarkProviderDispatchStartedAsync(
        LlmChatOperationId operationId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation> RecordInvocationAsync(
        LlmChatInvocationRecord record,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation> CompleteTranscriptAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        long resultingTranscriptRevision,
        Guid assistantEntryId,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation> RequestCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation> CompleteCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation> CompleteFailureAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        string failureCode,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation> RequireRecoveryAsync(
        LlmChatOperationId operationId,
        string failureCode,
        CancellationToken cancellationToken = default);
}
