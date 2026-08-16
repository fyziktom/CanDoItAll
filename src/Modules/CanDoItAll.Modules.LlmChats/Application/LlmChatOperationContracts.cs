using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed record SendLlmChatTurnCommand(
    LlmChatOperationId OperationId,
    LlmChatConversationId ConversationId,
    long ExpectedTranscriptRevision,
    string Message);

public sealed record AbandonLlmChatActiveTurnCommand(
    LlmChatConversationId ConversationId,
    LlmChatOperationId TurnId);

public sealed record LlmChatAssistantMessage(
    Guid EntryId,
    LlmChatOperationId TurnId,
    string Content,
    string Model,
    LlmUsage Usage,
    DateTimeOffset CreatedAtUtc);

public sealed record LlmChatOperationDetails(
    LlmChatOperation Operation,
    LlmChatAssistantMessage? AssistantMessage,
    IReadOnlyList<LlmChatInvocationRecord> Invocations)
{
    public const int MaximumInvocationRecords = 100;

    public bool Replayed { get; init; }

    public long LastEventSequence { get; init; }
}

public interface ILlmChatOperationApplicationService
{
    Task<Result<LlmChatOperationDetails>> SendAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatOperationDetails>> GetAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatOperationDetails>> CancelAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatOperationDetails>> AbandonActiveTurnAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken = default);
}
