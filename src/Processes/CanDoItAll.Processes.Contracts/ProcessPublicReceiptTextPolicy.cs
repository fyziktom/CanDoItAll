using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Contracts;

public static class ProcessPublicReceiptTextPolicy
{
    public const int MaximumPublicMessageCount = 32;
    public const int MaximumPublicMessageLength = 2_048;

    private const string PhysicalPathReplacement = "[physical path removed]";
    private const string TruncationSuffix = " [truncated]";
    private static readonly Regex HttpUserInfoRegex = new(
        @"(?<scheme>https?://)[^/\s?#@]+@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HttpParametersRegex = new(
        @"(?<uri>https?://[^\s?#]+)[?#][^\s]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool IsSafe(string? value, int maximumLength)
        => value is not null &&
           value.Length <= maximumLength &&
           string.Equals(value, Sanitize(value), StringComparison.Ordinal);

    public static string Sanitize(string? value)
    {
        var redacted = HttpParametersRegex.Replace(
            HttpUserInfoRegex.Replace(
                SensitiveTextRedactor.Redact(value),
                "${scheme}[credentials removed]@"),
            "${uri}[url parameters removed]");
        if (redacted.Length == 0)
        {
            return redacted;
        }

        var sanitized = new StringBuilder(redacted.Length);
        for (var index = 0; index < redacted.Length; index++)
        {
            if (!IsPhysicalPathStart(redacted, index))
            {
                sanitized.Append(redacted[index]);
                continue;
            }

            sanitized.Append(PhysicalPathReplacement);
            index = FindPhysicalPathEnd(redacted, index) - 1;
        }

        return sanitized.ToString();
    }

    public static IReadOnlyList<string> NormalizePublicMessages(IEnumerable<string?>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(Sanitize)
            .Select(static value => value.Trim())
            .Where(static value => value.Length > 0)
            .Select(BoundPublicMessage)
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumPublicMessageCount)
            .ToArray();
    }

    public static string NormalizePublicMessage(string? value, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        return NormalizePublicMessages([value]).FirstOrDefault() ?? fallback;
    }

    public static string? NormalizeOptionalPublicMessage(string? value)
        => NormalizePublicMessages([value]).FirstOrDefault();

    public static string NormalizePublicToken(
        string? value,
        string fallback,
        int maximumLength = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        if (maximumLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        var sanitized = Sanitize(value).Trim();
        var normalized = new string(sanitized
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':'
                ? character
                : '-')
            .ToArray())
            .Trim('-');
        if (normalized.Length == 0)
        {
            normalized = fallback;
        }

        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength].TrimEnd('-');
    }

    public static string? NormalizeOptionalPublicToken(string? value, int maximumLength = 128)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : NormalizePublicToken(value, "unknown", maximumLength);

    private static string BoundPublicMessage(string value)
    {
        if (value.Length <= MaximumPublicMessageLength)
        {
            return value;
        }

        return string.Concat(
            value.AsSpan(0, MaximumPublicMessageLength - TruncationSuffix.Length).TrimEnd(),
            TruncationSuffix);
    }

    private static bool IsPhysicalPathStart(string value, int index)
    {
        var remaining = value.AsSpan(index);
        if (remaining.Length >= 3 &&
            char.IsAsciiLetter(remaining[0]) &&
            remaining[1] == ':' &&
            remaining[2] is '\\' or '/')
        {
            return remaining[2] != '/' || !IsSafeRemoteUriSeparator(value, index + 2);
        }

        if (remaining.StartsWith(@"\\", StringComparison.Ordinal) ||
            remaining.StartsWith("//", StringComparison.Ordinal))
        {
            return !IsSafeRemoteUriSeparator(value, index);
        }

        return remaining[0] == '/' &&
               !IsSafeRemoteUriSeparator(value, index) &&
               (IsPathBoundaryBefore(value, index) || IsStandardUnixAbsolutePath(remaining));
    }

    private static bool IsStandardUnixAbsolutePath(ReadOnlySpan<char> value)
    {
        ReadOnlySpan<string> roots =
        [
            "/bin/", "/data/", "/dev/", "/etc/", "/home/", "/lib/", "/lib64/", "/media/",
            "/mnt/", "/opt/", "/private/", "/proc/", "/repositories/", "/root/", "/run/",
            "/sbin/", "/srv/", "/sys/", "/tmp/", "/usr/", "/Users/", "/var/", "/Volumes/",
            "/workspace/", "/workspaces/"
        ];
        foreach (var root in roots)
        {
            if (value.StartsWith(root, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPathBoundaryBefore(string value, int index)
        => index == 0 || !IsRelativePathTokenCharacter(value[index - 1]);

    private static bool IsRelativePathTokenCharacter(char value)
        => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '-';

    private static bool IsSafeRemoteUriSeparator(string value, int slashIndex)
    {
        var separatorIndex = slashIndex >= 2 &&
                             value[slashIndex - 1] == '/' &&
                             value[slashIndex - 2] == ':'
            ? slashIndex - 1
            : slashIndex;
        if (separatorIndex < 2 ||
            value[separatorIndex - 1] != ':' ||
            separatorIndex + 1 >= value.Length ||
            value[separatorIndex + 1] != '/')
        {
            return false;
        }

        var schemeStart = separatorIndex - 2;
        while (schemeStart > 0 && IsUriSchemeCharacter(value[schemeStart - 1]))
        {
            schemeStart--;
        }

        var scheme = value.AsSpan(schemeStart, separatorIndex - 1 - schemeStart);
        return scheme.Length > 0 &&
               char.IsAsciiLetter(scheme[0]) &&
               scheme[1..].ToArray().All(IsUriSchemeCharacter) &&
               (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                scheme.Equals("https", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUriSchemeCharacter(char value)
        => char.IsAsciiLetterOrDigit(value) || value is '+' or '-' or '.';

    private static int FindPhysicalPathEnd(string value, int startIndex)
    {
        var quote = startIndex > 0 && value[startIndex - 1] is '"' or '\''
            ? value[startIndex - 1]
            : '\0';
        for (var index = startIndex + 1; index < value.Length; index++)
        {
            if (quote != '\0' && value[index] == quote ||
                quote == '\0' && IsUnquotedPathTerminator(value[index]))
            {
                return index;
            }
        }

        return value.Length;
    }

    private static bool IsUnquotedPathTerminator(char value)
        => value is '\r' or '\n';
}
