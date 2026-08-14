namespace CanDoItAll.Modules.LlmChats.Common;

public readonly record struct LlmChatDefinitionId
{
    public LlmChatDefinitionId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static LlmChatDefinitionId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

public readonly record struct LlmChatDefinitionRevisionNumber
{
    public LlmChatDefinitionRevisionNumber(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Value = value;
    }

    public int Value { get; }

    public LlmChatDefinitionRevisionNumber Next()
        => new(checked(Value + 1));

    public override string ToString()
        => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct LlmChatConversationId
{
    public LlmChatConversationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static LlmChatConversationId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

public readonly record struct LlmChatOperationId
{
    public LlmChatOperationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static LlmChatOperationId New()
        => new(Guid.NewGuid());

    public Guid ToTurnId()
        => Value;

    public override string ToString()
        => Value.ToString("N");
}

public sealed record LlmChatRuntimeIdentity
{
    public LlmChatRuntimeIdentity(Guid profileId, string fingerprint, long generation)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(profileId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);

        ProfileId = profileId;
        Fingerprint = fingerprint.Trim();
        Generation = generation;
    }

    public Guid ProfileId { get; }

    public string Fingerprint { get; }

    public long Generation { get; }
}

public readonly record struct LlmChatRequestFingerprint
{
    public LlmChatRequestFingerprint(string value)
    {
        Value = LlmChatFingerprintValue.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

public readonly record struct LlmChatSettingsFingerprint
{
    public LlmChatSettingsFingerprint(string value)
    {
        Value = LlmChatFingerprintValue.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

internal static class LlmChatFingerprintValue
{
    public static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A fingerprint must be a 64-character SHA-256 hexadecimal value.", parameterName);
        }

        return normalized;
    }
}
