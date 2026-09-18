using System.Globalization;

namespace CanDoItAll.AgentFramework.UI.Chat;

public static class RelativeAttachmentPath {
    public const int MaximumLength = 1024;

    public static bool TryNormalize(string? value, out string relativePath) {
        relativePath = string.Empty;
        if (value is null) {
            return false;
        }

        var candidate = value.Trim();
        if (value.Any(character => char.GetUnicodeCategory(character) is UnicodeCategory.Control or UnicodeCategory.Format)) {
            return false;
        }
        if (candidate.Length is 0 or > MaximumLength || candidate[0] is '/' or '\\') {
            return false;
        }

        foreach (var character in candidate) {
            if (char.GetUnicodeCategory(character) is UnicodeCategory.Control or UnicodeCategory.Format or
                UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate ||
                character is ':' or '%' or '?' or '#' or '*' or '"' or '<' or '>' or '|' or
                '\u2044' or '\u2215' or '\u2216' or '\u2571' or '\u2572' or '\u29f5' or '\u29f8' or '\u29f9' or
                '\ufe68' or '\uff0f' or '\uff3c' or '\uff0e' or '\uff1a' or '\u2024') {
                return false;
            }
        }

        candidate = candidate.Replace('\\', '/');
        foreach (var segment in candidate.Split('/')) {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith('.') ||
                char.IsWhiteSpace(segment[0]) || char.IsWhiteSpace(segment[^1])) {
                return false;
            }
        }

        relativePath = candidate;
        return true;
    }

    public static string? NormalizeOrNull(string? value)
        => TryNormalize(value, out var relativePath) ? relativePath : null;
}
