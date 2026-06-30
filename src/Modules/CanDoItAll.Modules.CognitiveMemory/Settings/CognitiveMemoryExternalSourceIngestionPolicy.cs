using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.CognitiveMemory;

internal static class CognitiveMemoryExternalSourceIngestionPolicy
{
    private static readonly Regex SensitiveAssignmentRegex = new(
        @"(?im)^\s*(?:password|passwd|pwd|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|bearer|connectionstring|connection_string)\s*[:=]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PrivateKeyMarkerRegex = new(
        @"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE\s+KEY-----",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SensitiveQueryParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token",
        "api_key",
        "apikey",
        "client_secret",
        "code",
        "password",
        "secret",
        "token"
    };

    public static void EnsureContentAllowed(string contentText)
    {
        if (SensitiveAssignmentRegex.IsMatch(contentText) || PrivateKeyMarkerRegex.IsMatch(contentText))
        {
            throw new InvalidOperationException(
                "External source appears to contain sensitive credentials. Remove secrets or provide a redacted source before ingestion.");
        }
    }

    public static void EnsureUriAllowed(Uri uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return;
        }

        var query = uri.Query.TrimStart('?');
        var hasSensitiveParameter = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parameter => parameter.Split('=', 2)[0])
            .Any(SensitiveQueryParameterNames.Contains);
        if (hasSensitiveParameter)
        {
            throw new InvalidOperationException(
                "External source URL contains a sensitive query parameter. Remove secrets or use a redacted URL before ingestion.");
        }
    }

    public static string SafeLocatorForLog(string locator)
    {
        if (Uri.TryCreate(locator, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Path);
        }

        return Path.GetFileName(locator);
    }
}
