using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record ChatSessionTitleChange(Guid AgentId, Guid SessionId, string ExpectedTitle, string Title, DateTimeOffset ChangedAtUtc) {
    public ChatSessionRecord Apply(ChatSessionRecord current) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(ExpectedTitle);
        if (AgentId == Guid.Empty || SessionId == Guid.Empty || current.AgentId != AgentId || current.Id != SessionId) {
            throw new InvalidOperationException("The title change does not identify the selected agent's session.");
        }
        if (!string.Equals(current.Title, ExpectedTitle, StringComparison.Ordinal)) {
            throw new ChatSessionTitleConflictException(SessionId);
        }
        var title = Normalize(Title);
        return current.Title == title ? current : current with {
            Title = title,
            UpdatedAtUtc = ChangedAtUtc > current.UpdatedAtUtc ? ChangedAtUtc : current.UpdatedAtUtc
        };
    }

    public static string Normalize(string title) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var normalized = string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        const int maximumLength = 96;
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength].TrimEnd();
    }

    public static ChatSessionRecord? PreserveCurrentTitle(ChatSessionRecord? current, ChatSessionRecord? proposed) {
        if (current is null || proposed is null) {
            return proposed;
        }
        if (current.Id != proposed.Id || current.AgentId != proposed.AgentId) {
            throw new InvalidOperationException("A run publication cannot replace its selected chat session identity.");
        }
        return proposed with {
            Title = current.Title,
            UpdatedAtUtc = proposed.UpdatedAtUtc > current.UpdatedAtUtc ? proposed.UpdatedAtUtc : current.UpdatedAtUtc
        };
    }
}

public sealed class ChatSessionTitleConflictException(Guid sessionId)
    : InvalidOperationException("The thread title changed. Reload the thread before renaming it.") {
    public Guid SessionId { get; } = sessionId;
}
