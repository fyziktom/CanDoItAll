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

public sealed record LlmChatDefinitionReadModel(
    LlmChatDefinition Definition,
    LlmChatDefinitionRevision Revision,
    IReadOnlyList<string> Tags);

public interface ILlmChatDefinitionReadStore
{
    Task<LlmChatDefinitionReadModel?> TryGetAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default);

    Task<LlmChatPage<LlmChatDefinitionReadModel, LlmChatDefinitionCursor>> ListPageAsync(
        int take,
        LlmChatDefinitionCursor? cursor,
        LlmChatDefinitionStatus? status,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatConversationRepository
{
    Task<LlmChatConversation?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default);

    Task CreateAsync(LlmChatConversation conversation, CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        LlmChatConversation conversation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default);
}

public sealed record LlmChatConversationReadModel(
    LlmChatConversation Conversation,
    string DefinitionName,
    LlmChatConversationEngineState Transcript);

public sealed record LlmChatTranscriptReadModel(
    LlmChatConversationReadModel Conversation,
    IReadOnlyList<LlmChatTranscriptEntry> Entries,
    LlmChatTranscriptCursor? NextCursor);

public interface ILlmChatConversationReadStore
{
    Task<LlmChatConversationReadModel?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default);

    Task<LlmChatPage<LlmChatConversationReadModel, LlmChatConversationCursor>> ListPageAsync(
        int take,
        LlmChatConversationCursor? cursor,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default);

    Task<LlmChatTranscriptReadModel?> TryGetTranscriptPageAsync(
        LlmChatConversationId id,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationTurnEvidence?> TryInspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatOperationRepository
{
    Task<LlmChatOperation?> TryGetAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperation?> TryGetForUpdateAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default);

    Task<LlmChatOperationAdmission> AdmitAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken = default);

    Task<bool> TryReplaceAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default);

    Task<bool> TryReplaceOwnedAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        LlmChatExecutionLeaseIdentity executionLease,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record LlmChatOperationReadModel(
    LlmChatOperation Operation,
    IReadOnlyList<LlmChatInvocationRecord> Invocations);

public interface ILlmChatOperationReadStore
{
    Task<LlmChatOperationReadModel?> TryGetAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmChatOperationId>> ListDispatchCandidatesAsync(
        DateTimeOffset observedAtUtc,
        int take,
        CancellationToken cancellationToken = default);
}

public sealed record LlmChatOperationAdmission(
    LlmChatOperation Operation,
    bool Created);

public sealed record LlmChatConversationTurnState(
    bool Exists,
    bool HasActiveTurn,
    bool HasNonterminalOperation);

public interface ILlmChatTurnStateRepository
{
    Task<LlmChatConversationTurnState> LockAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default);
}

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

public interface ILlmChatCommitFence
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
