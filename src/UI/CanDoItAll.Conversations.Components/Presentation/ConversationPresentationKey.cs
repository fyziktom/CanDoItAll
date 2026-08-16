namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationPresentationKey
{
    public ConversationPresentationKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}
