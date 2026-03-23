namespace CanDoItAll.ComponentKit.Canvas;

public sealed record TextMeasureResult(
    int EstimatedWidth,
    int EstimatedHeight,
    int LineCount,
    string DisplayText,
    bool IsTruncated);

public sealed class TextMeasureService
{
    private const int DefaultCharacterWidth = 8;
    private const int DefaultLineHeight = 20;

    public TextMeasureResult Measure(string? text, int maxLineLength, int maxLines = 1)
    {
        var normalized = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (normalized.Length == 0)
        {
            return new TextMeasureResult(0, DefaultLineHeight, 1, string.Empty, false);
        }

        var allowedLineLength = Math.Max(1, maxLineLength);
        var allowedLines = Math.Max(1, maxLines);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrWhiteSpace(currentLine)
                ? word
                : $"{currentLine} {word}";

            if (candidate.Length <= allowedLineLength)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentLine))
            {
                lines.Add(currentLine);
            }

            currentLine = word.Length <= allowedLineLength
                ? word
                : FitWithEllipsis(word, allowedLineLength);

            if (lines.Count >= allowedLines)
            {
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentLine) && lines.Count < allowedLines)
        {
            lines.Add(currentLine);
        }

        if (lines.Count == 0)
        {
            lines.Add(FitWithEllipsis(normalized, allowedLineLength));
        }

        var truncated = words.Length > 0 &&
            string.Join(' ', lines).Length < normalized.Length;
        if (truncated)
        {
            lines[^1] = FitWithEllipsis(lines[^1], allowedLineLength);
        }

        var longestLine = lines.Max(line => line.Length);
        return new TextMeasureResult(
            longestLine * DefaultCharacterWidth,
            lines.Count * DefaultLineHeight,
            lines.Count,
            string.Join(Environment.NewLine, lines),
            truncated);
    }

    public string FitWithEllipsis(string? text, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (normalized.Length <= Math.Max(1, maxLength))
        {
            return normalized;
        }

        if (maxLength <= 1)
        {
            return normalized[..1];
        }

        return $"{normalized[..(maxLength - 1)].TrimEnd()}…";
    }
}
