namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationMessagePresentation
{
    public ConversationMessagePresentation(
        ConversationPresentationKey key,
        ConversationMessageRole role,
        string roleLabel,
        PresentationTone roleTone,
        string content,
        string createdAtDisplay,
        string? hiddenContext = null,
        string? copyValue = null,
        string? copyAriaLabel = null,
        int tokenEstimate = 0,
        ConversationAvatarPresentation? avatar = null,
        ConversationMessageState state = ConversationMessageState.Normal)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdAtDisplay);
        Key = key;
        Role = role;
        RoleLabel = roleLabel;
        RoleTone = roleTone;
        Content = content ?? string.Empty;
        CreatedAtDisplay = createdAtDisplay;
        HiddenContext = hiddenContext;
        CopyValue = copyValue;
        CopyAriaLabel = copyAriaLabel;
        TokenEstimate = Math.Max(0, tokenEstimate);
        Avatar = avatar;
        State = state;
    }

    public ConversationPresentationKey Key { get; }

    public ConversationMessageRole Role { get; }

    public string RoleLabel { get; }

    public PresentationTone RoleTone { get; }

    public string Content { get; }

    public string CreatedAtDisplay { get; }

    public string? HiddenContext { get; }

    public string? CopyValue { get; }

    public string? CopyAriaLabel { get; }

    public int TokenEstimate { get; }

    public ConversationAvatarPresentation? Avatar { get; }

    public ConversationMessageState State { get; }
}
