namespace CanDoItAll.SharedKernel;

public sealed class LogicalPath : IEquatable<LogicalPath>
{
    private const int MaximumPathLength = 4096;
    private const int MaximumSegmentLength = 255;

    private LogicalPath(string value, IReadOnlyList<string> segments)
    {
        Value = value;
        Segments = segments;
    }

    public string Value { get; }

    public IReadOnlyList<string> Segments { get; }

    public static LogicalPath Parse(string value)
    {
        return ParseCore(value, convertLegacyWindowsSeparators: false);
    }

    public static LogicalPath ParseLegacyWindowsLogicalPath(string value)
    {
        return ParseCore(value, convertLegacyWindowsSeparators: true);
    }

    public static bool TryParse(string? value, out LogicalPath? path)
    {
        return TryParseCore(value, convertLegacyWindowsSeparators: false, out path);
    }

    public bool Equals(LogicalPath? other)
    {
        return other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is LogicalPath other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Value);
    }

    public override string ToString()
    {
        return Value;
    }

    private static LogicalPath ParseCore(string value, bool convertLegacyWindowsSeparators)
    {
        if (!TryParseCore(value, convertLegacyWindowsSeparators, out var path))
        {
            throw new ArgumentException(
                $"Value '{SanitizeForMessage(value)}' is not a valid logical path.",
                nameof(value));
        }

        return path!;
    }

    private static bool TryParseCore(
        string? value,
        bool convertLegacyWindowsSeparators,
        out LogicalPath? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumPathLength ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = convertLegacyWindowsSeparators
            ? value.Replace('\\', '/')
            : value;
        if (candidate[0] == '/' ||
            candidate[^1] == '/' ||
            candidate.Contains('\\') ||
            candidate.Contains(':') ||
            candidate.Any(char.IsControl) ||
            HasInvalidUnicode(candidate))
        {
            return false;
        }

        var segments = candidate.Split('/');
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment.Length > MaximumSegmentLength ||
                string.Equals(segment, ".", StringComparison.Ordinal) ||
                string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            return false;
        }

        path = new LogicalPath(candidate, Array.AsReadOnly(segments));
        return true;
    }

    private static bool HasInvalidUnicode(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsSurrogate(value[index]))
            {
                continue;
            }

            if (!char.IsHighSurrogate(value[index]) ||
                index + 1 >= value.Length ||
                !char.IsLowSurrogate(value[index + 1]))
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static string SanitizeForMessage(string? value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Concat(value.Select(character => char.IsControl(character) ? '?' : character));
    }
}
