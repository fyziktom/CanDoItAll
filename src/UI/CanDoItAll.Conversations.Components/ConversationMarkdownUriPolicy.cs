using System.Net;

namespace CanDoItAll.Conversations.Components;

internal static class ConversationMarkdownUriPolicy
{
    private const string InertUri = "about:blank";
    private const string HttpScheme = "http";
    private const string HttpsScheme = "https";
    private const string MailtoScheme = "mailto";
    private const int MaximumDecodePasses = 3;

    public static string Rewrite(string uri)
        => IsAllowed(uri) ? uri : InertUri;

    private static bool IsAllowed(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        var inspected = DecodeForInspection(uri);
        if (inspected is null)
        {
            return false;
        }

        inspected = string.Concat(inspected.Where(character =>
            !char.IsControl(character) &&
            !char.IsWhiteSpace(character)));
        if (inspected.Length == 0 || inspected.StartsWith("//", StringComparison.Ordinal) || inspected.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return false;
        }

        var colonIndex = inspected.IndexOf(':');
        var relativeDelimiterIndex = inspected.IndexOfAny(['/', '?', '#']);
        if (colonIndex < 0 || relativeDelimiterIndex >= 0 && relativeDelimiterIndex < colonIndex)
        {
            return true;
        }

        return inspected[..colonIndex].ToLowerInvariant() switch
        {
            HttpScheme or HttpsScheme or MailtoScheme => true,
            _ => false
        };
    }

    private static string? DecodeForInspection(string uri)
    {
        var decoded = WebUtility.HtmlDecode(uri).Trim();
        try
        {
            for (var pass = 0; pass < MaximumDecodePasses; pass++)
            {
                var next = Uri.UnescapeDataString(decoded);
                if (string.Equals(next, decoded, StringComparison.Ordinal))
                {
                    break;
                }

                decoded = next;
            }

            return decoded;
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
