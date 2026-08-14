using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;

namespace CanDoItAll.Modules.LlmChats.Conversations;

public enum LlmChatConversationStatus
{
    Active,
    Archived
}

public enum LlmChatConversationOrigin
{
    Application,
    Api
}

public static class LlmChatConversationTitlePolicy
{
    public const string DefaultTitle = "New chat";

    public static string Normalize(string? title)
    {
        var normalized = title?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return DefaultTitle;
        }

        if (normalized.Length > LlmConversationDocument.MaximumTitleLength)
        {
            throw new ArgumentException(
                $"A conversation title cannot exceed {LlmConversationDocument.MaximumTitleLength} characters.",
                nameof(title));
        }

        return normalized;
    }

    public static string FromFirstMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var firstLine = message.ReplaceLineEndings("\n").Split('\n', 2)[0].Trim();
        if (firstLine.Length == 0)
        {
            return DefaultTitle;
        }

        return firstLine.Length <= LlmConversationDocument.MaximumTitleLength
            ? firstLine
            : firstLine[..LlmConversationDocument.MaximumTitleLength].TrimEnd();
    }
}

public sealed record LlmChatConversation
{
    public LlmChatConversation(
        LlmChatConversationId id,
        LlmChatDefinitionId definitionId,
        LlmChatDefinitionRevisionNumber definitionRevision,
        string title,
        LlmChatConversationStatus status,
        LlmChatConversationOrigin origin,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long concurrencyToken)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("A conversation requires an id.", nameof(id));
        }

        if (definitionId.Value == Guid.Empty)
        {
            throw new ArgumentException("A conversation requires a definition id.", nameof(definitionId));
        }

        if (definitionRevision.Value < 1)
        {
            throw new ArgumentException("A conversation requires a definition revision.", nameof(definitionRevision));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown conversation status.");
        }

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown conversation origin.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(concurrencyToken);
        if (updatedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Updated time cannot precede created time.", nameof(updatedAtUtc));
        }

        Id = id;
        DefinitionId = definitionId;
        DefinitionRevision = definitionRevision;
        Title = LlmChatConversationTitlePolicy.Normalize(title);
        Status = status;
        Origin = origin;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        ConcurrencyToken = concurrencyToken;
    }

    public LlmChatConversationId Id { get; }

    public LlmChatDefinitionId DefinitionId { get; }

    public LlmChatDefinitionRevisionNumber DefinitionRevision { get; }

    public string Title { get; }

    public LlmChatConversationStatus Status { get; }

    public LlmChatConversationOrigin Origin { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public long ConcurrencyToken { get; }
}
