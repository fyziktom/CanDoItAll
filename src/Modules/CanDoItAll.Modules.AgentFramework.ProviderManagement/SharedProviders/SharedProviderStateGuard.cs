using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal static class SharedProviderStateGuard
{
    public static string NormalizeText(string value, int maximumLength, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 ||
            normalized.Length > maximumLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                "The value is empty, too long, or contains control characters.",
                parameterName);
        }

        return normalized;
    }

    public static string ExactText(string value, int maximumLength, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (value.Length == 0 ||
            value.Length > maximumLength ||
            value != value.Trim() ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "The value is empty, too long, padded, or contains control characters.",
                parameterName);
        }

        return value;
    }

    public static void PublicationId(
        SharedProviderPublicationId publicationId,
        string parameterName)
    {
        if (publicationId.Value == Guid.Empty)
        {
            throw new ArgumentException("A publication id cannot be empty.", parameterName);
        }
    }

    public static void SourceInstanceId(
        SharedProviderSourceInstanceId sourceInstanceId,
        string parameterName)
    {
        if (sourceInstanceId.Value == Guid.Empty)
        {
            throw new ArgumentException("A source-instance id cannot be empty.", parameterName);
        }
    }

    public static void EntityTag(
        SharedProviderCatalogEntityTag entityTag,
        string parameterName)
    {
        if (string.IsNullOrEmpty(entityTag.Value))
        {
            throw new ArgumentException("A catalog entity tag cannot be empty.", parameterName);
        }
    }

    public static void NonEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An identity cannot be empty.", parameterName);
        }
    }

    public static void Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A timestamp must use the UTC offset.", parameterName);
        }
    }

    public static void TransitionTimestamp(
        DateTimeOffset timestampUtc,
        DateTimeOffset currentTimestampUtc,
        string parameterName)
    {
        Utc(timestampUtc, parameterName);
        if (timestampUtc < currentTimestampUtc)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A state transition timestamp cannot move backwards.");
        }
    }
}
