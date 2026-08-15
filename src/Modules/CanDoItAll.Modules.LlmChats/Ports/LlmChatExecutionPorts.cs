using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Ports;

public sealed record LlmChatConversationEngineState(
    LlmChatConversationId ConversationId,
    long TranscriptRevision,
    bool HasActiveTurn,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LlmChatTranscriptEntry(
    Guid EntryId,
    Guid TurnId,
    LlmMessageRole Role,
    string Text,
    DateTimeOffset CreatedAtUtc,
    string Model,
    LlmUsage? Usage);

public sealed record LlmChatTranscriptPage(
    LlmChatConversationEngineState State,
    IReadOnlyList<LlmChatTranscriptEntry> Entries,
    int? NextOffset);

public sealed record LlmChatConversationEngineTurnResult(
    LlmChatConversationEngineState State,
    LlmChatOperationId OperationId,
    Guid AssistantEntryId,
    string AssistantText,
    string Model,
    LlmUsage Usage);

public sealed record LlmChatAssistantTurnEvidence(
    Guid EntryId,
    string Text,
    string Model,
    LlmUsage Usage,
    DateTimeOffset CreatedAtUtc);

public sealed record LlmChatConversationTurnEvidence(
    LlmChatConversationEngineState State,
    LlmChatOperationId OperationId,
    bool HasExactActiveTurn,
    LlmChatAssistantTurnEvidence? Assistant);

public interface ILlmChatConversationEngine
{
    Task<LlmChatConversationEngineState> CreateAsync(
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision definitionRevision,
        string title,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationEngineState?> TryGetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default);

    Task<LlmChatTranscriptPage?> TryGetTranscriptPageAsync(
        LlmChatConversationId conversationId,
        int take,
        int offset,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationEngineState> RenameAsync(
        LlmChatConversationId conversationId,
        string title,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationEngineTurnResult> SendAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default);

    Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default);

    Task<LlmInvocationResult> InvokeTurnAsync(
        LlmConversationTurnAdmission admission,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationEngineTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationEngineState> CompensateTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationTurnEvidence?> InspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);

    Task<LlmChatConversationEngineState> AbandonActiveTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatRuntimeLeaseFactory
{
    ValueTask<ILlmChatRuntimeLease> AcquireAsync(CancellationToken cancellationToken = default);
}

public interface ILlmChatRuntimeLease : IAsyncDisposable
{
    LlmChatRuntimeIdentity Identity { get; }

    CancellationToken CancellationToken { get; }

    Result EnsureCurrent();
}

public sealed class LlmChatRuntimeProfileChangedException() :
    OperationCanceledException("The active database profile changed during LLM Chat execution.")
{
    public string Code { get; } = LlmChatErrorCodes.RuntimeProfileChanged;
}

public sealed class LlmChatConversationEngineException(
    string code,
    string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record LlmChatOperationExecutionContext(
    LlmChatOperationId OperationId,
    LlmChatRuntimeIdentity RuntimeIdentity);

public interface ILlmChatOperationScopeAccessor
{
    LlmChatOperationExecutionContext? Current { get; }

    IDisposable Push(LlmChatOperationExecutionContext context);
}

public interface ILlmChatOperationCancellationRegistration : IDisposable
{
    CancellationToken CancellationToken { get; }
}

public interface ILlmChatOperationCancellationRegistry
{
    ILlmChatOperationCancellationRegistration Register(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken);

    bool RequestCancellation(LlmChatOperationId operationId);

    bool IsRegistered(LlmChatOperationId operationId);
}
