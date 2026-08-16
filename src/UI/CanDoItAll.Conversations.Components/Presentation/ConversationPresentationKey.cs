namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationPresentationKey
{
    public const int MaximumLength = 256;

    public ConversationPresentationKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"Conversation presentation keys cannot exceed {MaximumLength} characters.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}
