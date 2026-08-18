using CanDoItAll.AgentFramework.Llm.Abstractions;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;

public static class LlmChatDefinitionValidation
{
    public const int MaximumTags = 20;
    public const int MaximumTagLength = 100;
    public const int MaximumNameLength = 200;
    public const int MaximumSummaryLength = 2_000;
    public const int MaximumAvatarImageUrlLength = 2_048;
    public const int MaximumProviderNameLength = 200;
    public const int MaximumModelLength = 200;
    public const int MaximumRevisionReasonLength = 500;
    public static readonly TimeSpan MaximumTimeout = LlmInvocationRequest.MaximumTimeout;

    public static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    public static string NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    public static string NormalizeAvatarImageUrl(string? value)
    {
        var normalized = NormalizeOptional(value, MaximumAvatarImageUrlLength, nameof(value));
        if (normalized.Length == 0)
        {
            return normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("An avatar image URL must be an absolute HTTP or HTTPS URL.", nameof(value));
        }

        return normalized;
    }

    public static void ValidateSettings(LlmModelSettings settings, TimeSpan? timeout)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Temperature is { } temperature &&
            (!double.IsFinite(temperature) || temperature is < 0 or > 2))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), temperature, "Temperature must be between 0 and 2.");
        }

        if (settings.ThinkingEffort is { } thinkingEffort && !Enum.IsDefined(thinkingEffort))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), thinkingEffort, "Unknown thinking effort.");
        }

        if (timeout is { } deadline && (deadline <= TimeSpan.Zero || deadline > MaximumTimeout))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), deadline, $"Timeout must be positive and at most {MaximumTimeout}.");
        }
    }

    public static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        if (tags.Count > MaximumTags)
        {
            throw new ArgumentException($"A definition cannot have more than {MaximumTags} tags.", nameof(tags));
        }

        var normalized = tags
            .Select(tag => NormalizeRequired(tag, MaximumTagLength, nameof(tags)).ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length != tags.Count)
        {
            throw new ArgumentException("Definition tags must be unique after normalization.", nameof(tags));
        }

        return normalized;
    }
}
