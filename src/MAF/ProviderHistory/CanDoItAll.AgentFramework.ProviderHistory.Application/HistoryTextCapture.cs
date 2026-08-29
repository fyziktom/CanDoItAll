using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.ProviderHistory;

public static class HistoryTextCapture {
    private const string Redacted = "[redacted]";
    private static readonly Regex CredentialPattern = new(
        """(?i)(?:bearer\s+[a-z0-9._~+/-]+=*|(?:api[_-]?key|password|client[_-]?secret|authorization)\s*[:=]\s*["']?[^\s"',;}]+)""",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    public static HistoryCapturedText Capture(string text, int maximumBytes, IReadOnlyList<string> knownSecrets) {
        ArgumentNullException.ThrowIfNull(text);
        if (maximumBytes is < 1 or > 128 * 1024 || knownSecrets.Count > 128 ||
            knownSecrets.Any(secret => secret.Length > 128 * 1024)) {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        var longestSecret = knownSecrets.Count == 0 ? 0 : knownSecrets.Max(secret => secret.Length);
        var prefixLength = Math.Min(text.Length, checked(maximumBytes + longestSecret + 1024));
        if (prefixLength < text.Length && prefixLength > 0 && char.IsHighSurrogate(text[prefixLength - 1])) {
            prefixLength--;
        }
        var prefix = text[..prefixLength];
        var redacted = prefix;
        foreach (var secret in knownSecrets.OrderByDescending(secret => secret.Length)) {
            if (secret.Length > 0) {
                redacted = redacted.Replace(secret, Redacted, StringComparison.Ordinal);
            }
        }
        redacted = CredentialPattern.Replace(redacted, Redacted);
        var flags = HistoryDetailFlags.PriorContextNotCaptured;
        if (!string.Equals(prefix, redacted, StringComparison.Ordinal)) {
            flags |= HistoryDetailFlags.Redacted;
        }

        if (prefixLength < text.Length) {
            var safeLength = Math.Max(0, redacted.Length - longestSecret);
            if (safeLength > 0 && char.IsHighSurrogate(redacted[safeLength - 1])) {
                safeLength--;
            }
            redacted = redacted[..safeLength];
        }

        var builder = new StringBuilder(Math.Min(redacted.Length, maximumBytes));
        var capturedBytes = 0;
        Span<char> buffer = stackalloc char[2];
        foreach (var rune in redacted.EnumerateRunes()) {
            if (capturedBytes + rune.Utf8SequenceLength > maximumBytes) {
                flags |= HistoryDetailFlags.Truncated;
                break;
            }
            var characters = rune.EncodeToUtf16(buffer);
            builder.Append(buffer[..characters]);
            capturedBytes += rune.Utf8SequenceLength;
        }
        if (prefixLength < text.Length) {
            flags |= HistoryDetailFlags.Truncated;
        }
        return new(builder.ToString(), Encoding.UTF8.GetByteCount(text), capturedBytes, flags);
    }
}
