using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Memory.Application;

internal static partial class ManualMemorySourceSafetyPolicy
{
    public static void EnsureContentAllowed(string contentText)
    {
        if (MemorySourceSnapshotSecurity.ContainsSensitiveInlineValue(contentText) ||
            PrivateKeyMarkerRegex().IsMatch(contentText))
        {
            throw new InvalidOperationException(
                "Manual source content appears to contain sensitive credentials. Remove secrets or provide a redacted source before ingestion.");
        }
    }

    public static Uri EnsureUriAllowed(string locator)
    {
        if (!Uri.TryCreate(locator, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Manual link sources must use an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return uri;
        }

        var query = uri.Query.TrimStart('?');
        var hasSensitiveParameter = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parameter => parameter.Split('=', 2)[0])
            .Any(MemorySourceSnapshotSecurity.IsSensitiveQueryParameterName);
        if (hasSensitiveParameter)
        {
            throw new InvalidOperationException(
                "Manual source URL contains a sensitive query parameter. Remove secrets or use a redacted URL before ingestion.");
        }

        return uri;
    }

    public static string RedactText(string value)
        => MemorySourceSnapshotSecurity.RedactSensitiveInlineValues(value);

    public static string SafeLocatorForTitle(string locator)
    {
        if (Uri.TryCreate(locator, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Path);
        }

        return Path.GetFileName(locator);
    }

    [GeneratedRegex(@"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE\s+KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyMarkerRegex();
}
