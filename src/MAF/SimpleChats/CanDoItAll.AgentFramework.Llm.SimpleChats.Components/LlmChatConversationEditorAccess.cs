using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public interface ILlmChatConversationEditorAccess {
    Task<LlmChatEditorOpening> OpenAsync(Guid? conversationId, LlmChatConversationAccessKind kind, Guid editorId,
        Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken);
}

public enum LlmChatConversationAccessKind { Read, Metadata, Execute, Recovery }

public sealed class UnavailableLlmChatConversationEditorAccess : ILlmChatConversationEditorAccess {
    public Task<LlmChatEditorOpening> OpenAsync(Guid? conversationId, LlmChatConversationAccessKind kind, Guid editorId,
        Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken)
        => Task.FromException<LlmChatEditorOpening>(new UnauthorizedAccessException("Original conversation access is unavailable in this host."));
}
