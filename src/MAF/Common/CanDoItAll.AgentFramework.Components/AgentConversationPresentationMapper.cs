using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Components;

internal static class AgentConversationPresentationMapper
{
    private const string UserRequestMarker = "User request:";

    public static ConversationHeaderPresentation MapHeader(
        AgentDefinition? agent,
        IReadOnlyList<PresentationBadge> badges)
        => new(MapAvatar(agent), badges);

    public static IReadOnlyList<ConversationMessagePresentation> MapMessages(
        AgentDefinition? agent,
        IEnumerable<ChatMessageRecord> messages)
        => messages
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => MapMessage(agent, message))
            .ToArray();

    public static ConversationMessagePresentation MapPendingMessage(
        AgentDefinition? agent,
        string content,
        DateTimeOffset createdAt)
    {
        var display = ResolveUserMessageDisplay(content);
        return new(
            key: new("pending-user-message"),
            role: ConversationMessageRole.User,
            roleLabel: "User",
            roleTone: PresentationTone.Info,
            content: display.VisibleContent,
            createdAtDisplay: createdAt.LocalDateTime.ToString("g"),
            hiddenContext: display.HiddenContext,
            avatar: MapAvatar(agent),
            state: ConversationMessageState.Pending);
    }

    public static PresentationTone MapTone(string? tone)
        => tone?.Trim().ToLowerInvariant() switch
        {
            "primary" => PresentationTone.Accent,
            "info" => PresentationTone.Info,
            "success" => PresentationTone.Success,
            "warning" => PresentationTone.Warning,
            "danger" => PresentationTone.Danger,
            _ => PresentationTone.Default
        };

    private static ConversationMessagePresentation MapMessage(
        AgentDefinition? agent,
        ChatMessageRecord message)
    {
        var isUser = message.Role == ChatMessageRole.User;
        var display = isUser
            ? ResolveUserMessageDisplay(message.Content)
            : new UserMessageDisplay(message.Content, null);
        var role = message.Role switch
        {
            ChatMessageRole.User => ConversationMessageRole.User,
            ChatMessageRole.Assistant => ConversationMessageRole.Assistant,
            _ => ConversationMessageRole.Other
        };
        var tone = role switch
        {
            ConversationMessageRole.User => PresentationTone.Info,
            ConversationMessageRole.Assistant => PresentationTone.Success,
            _ => PresentationTone.Default
        };

        return new(
            key: new(message.Id.ToString("N")),
            role: role,
            roleLabel: message.Role.ToString(),
            roleTone: tone,
            content: display.VisibleContent,
            createdAtDisplay: message.CreatedAtUtc.LocalDateTime.ToString("g"),
            hiddenContext: display.HiddenContext,
            copyValue: message.Content,
            copyAriaLabel: isUser ? "Copy user message" : "Copy assistant message",
            tokenEstimate: message.TokenEstimate,
            avatar: isUser ? null : MapAvatar(agent));
    }

    private static ConversationAvatarPresentation MapAvatar(AgentDefinition? agent)
    {
        var seed = ResolveAvatarSeed(agent);
        return new(
            agent?.Name ?? "Selected agent",
            agent?.AvatarImageUrl,
            BuildInitials(seed),
            seed);
    }

    private static string ResolveAvatarSeed(AgentDefinition? agent)
    {
        if (!string.IsNullOrWhiteSpace(agent?.Name))
        {
            return agent.Name;
        }

        return string.IsNullOrWhiteSpace(agent?.RoleTitle)
            ? "Sandbox Agent"
            : agent.RoleTitle;
    }

    private static string BuildInitials(string value)
    {
        var segments = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToArray();
        return segments.Length == 0
            ? "AG"
            : string.Concat(segments.Select(segment => char.ToUpperInvariant(segment[0])));
    }

    private static UserMessageDisplay ResolveUserMessageDisplay(string? content)
    {
        var normalizedContent = content?.Trim() ?? string.Empty;
        if (normalizedContent.Length == 0)
        {
            return new(string.Empty, null);
        }

        var markerIndex = normalizedContent.LastIndexOf(UserRequestMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return new(normalizedContent, null);
        }

        var visibleContent = normalizedContent[(markerIndex + UserRequestMarker.Length)..].Trim();
        if (visibleContent.Length == 0)
        {
            return new(normalizedContent, null);
        }

        var hiddenContext = normalizedContent[..markerIndex].Trim();
        return new(visibleContent, hiddenContext.Length == 0 ? null : hiddenContext);
    }

    private sealed record UserMessageDisplay(string VisibleContent, string? HiddenContext);
}
