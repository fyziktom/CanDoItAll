using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Components;

internal static class AgentThreadHistoryPresentationMapper
{
    public static ConversationThreadPresentation Map(
        ChatSessionSummaryRecord session,
        Guid? selectedSessionId)
    {
        return Create(
            session.Id,
            session.Title,
            session.UpdatedAtUtc,
            session.MessageCount,
            session.LastMessagePreview,
            session.PendingApprovalCount,
            hasRunEvidence: false,
            selectedSessionId);
    }

    public static ConversationThreadPresentation Map(
        ChatSessionRecord session,
        Guid? selectedSessionId)
    {
        return Create(
            session.Id,
            session.Title,
            session.UpdatedAtUtc,
            session.Messages.Count,
            ResolvePreview(session),
            session.Compatibility?.PendingApprovals.Count ?? 0,
            session.LatestExecutionRunId.HasValue,
            selectedSessionId);
    }

    public static bool TryResolveSessionId(
        ConversationPresentationKey key,
        out Guid sessionId)
        => Guid.TryParseExact(key.Value, "N", out sessionId);

    private static ConversationThreadPresentation Create(
        Guid sessionId,
        string title,
        DateTimeOffset updatedAtUtc,
        int messageCount,
        string preview,
        int pendingApprovalCount,
        bool hasRunEvidence,
        Guid? selectedSessionId)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? $"Thread {updatedAtUtc.LocalDateTime:g}"
            : title;
        var badges = new List<PresentationBadge>();
        if (selectedSessionId == sessionId)
        {
            badges.Add(new("Open", PresentationTone.Accent));
        }

        if (hasRunEvidence)
        {
            badges.Add(new("Run evidence", PresentationTone.Info));
        }

        if (pendingApprovalCount > 0)
        {
            badges.Add(new($"{pendingApprovalCount} approvals", PresentationTone.Warning));
        }

        badges.Add(new($"{messageCount} messages"));

        return new(
            key: new(sessionId.ToString("N")),
            title: resolvedTitle,
            updatedAtUtc: updatedAtUtc,
            updatedAtDisplay: updatedAtUtc.LocalDateTime.ToString("g"),
            metadata: messageCount == 0 ? "Empty thread" : $"{messageCount} messages",
            preview: string.IsNullOrWhiteSpace(preview) ? "No messages captured yet." : preview,
            searchText: string.Join(' ', resolvedTitle, preview),
            selectLabel: $"Open thread {resolvedTitle}",
            badges: badges,
            isSelected: selectedSessionId == sessionId);
    }

    private static string ResolvePreview(ChatSessionRecord session)
    {
        var latestMessage = session.Messages
            .OrderByDescending(message => message.CreatedAtUtc)
            .FirstOrDefault();
        if (latestMessage is null || string.IsNullOrWhiteSpace(latestMessage.Content))
        {
            return "No messages captured yet.";
        }

        var preview = string.Join(
            ' ',
            latestMessage.Content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        const int maxLength = 170;
        return preview.Length <= maxLength
            ? preview
            : $"{preview[..maxLength].TrimEnd()}...";
    }
}
