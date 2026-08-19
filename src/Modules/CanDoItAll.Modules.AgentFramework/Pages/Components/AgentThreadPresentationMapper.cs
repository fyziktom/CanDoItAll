using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class AgentThreadPresentationMapper
{
    public static ConversationThreadPresentation Map(
        ChatSessionSummaryRecord session,
        Guid? selectedSessionId)
    {
        var metadata = session.MessageCount == 0
            ? "Empty thread"
            : $"{session.MessageCount} message(s)";
        var tooltipText = NormalizeInlineText(session.LastMessagePreview);
        var preview = Truncate(tooltipText, 88);
        var badges = new List<PresentationBadge>();
        if (session.PendingApprovalCount > 0)
        {
            badges.Add(new($"{session.PendingApprovalCount} approval(s)", PresentationTone.Warning));
        }

        if (session.AutoApprovePendingToolCalls)
        {
            badges.Add(new("Auto approve", PresentationTone.Success));
        }

        return new(
            key: ThreadKey(session.Id),
            title: session.Title,
            updatedAtUtc: session.UpdatedAtUtc,
            updatedAtDisplay: session.UpdatedAtUtc.LocalDateTime.ToString("dd.MM HH:mm"),
            metadata: metadata,
            preview: preview,
            searchText: string.Join(' ', session.Title, session.LastMessagePreview, metadata),
            tooltipText: tooltipText,
            selectLabel: $"Open thread {session.Title}",
            badges: badges,
            isSelected: selectedSessionId == session.Id);
    }

    public static bool TryResolveSessionId(
        ConversationPresentationKey key,
        out Guid sessionId)
        => Guid.TryParseExact(key.Value, "N", out sessionId);

    private static ConversationPresentationKey ThreadKey(Guid sessionId)
        => new(sessionId.ToString("N"));

    private static string NormalizeInlineText(string value)
        => string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength
            ? value
            : $"{value[..maxLength].TrimEnd()}...";
}
