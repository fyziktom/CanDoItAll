using System.Globalization;

namespace CanDoItAll.Components.CanvasLib;

public sealed class TextMeasureFontSpec
{
    public static TextMeasureFontSpec Default { get; } = new();

    public string Family { get; init; } = "\"DM Sans\", sans-serif";

    public double SizePx { get; init; } = 12;

    public int Weight { get; init; } = 600;

    public double LineHeightPx { get; init; } = 18;

    public double LetterSpacingPx { get; init; }

    public string ToCssFontShorthand()
        => $"{Weight} {SizePx.ToString("0.##", CultureInfo.InvariantCulture)}px {Family}";
}

public sealed class TextMeasureRequest
{
    public string Id { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public TextMeasureFontSpec Font { get; init; } = TextMeasureFontSpec.Default;

    public double MaxWidth { get; init; } = 160;

    public int MaxLines { get; init; } = 1;

    public string TruncationMode { get; init; } = "ellipsis";

    public bool IsReadOnly { get; init; }

    public bool IsDisabled { get; init; }
}

public sealed record TextMeasureLineResult(
    int Index,
    string Text,
    int EstimatedWidth,
    bool IsEllipsized);

public sealed class TextMeasureResult
{
    public TextMeasureResult(
        int estimatedWidth,
        int estimatedHeight,
        int lineCount,
        string displayText,
        bool isTruncated)
        : this(
            estimatedWidth,
            estimatedHeight,
            lineCount,
            displayText,
            isTruncated,
            displayText,
            BuildFallbackLines(displayText, estimatedWidth, isTruncated))
    {
    }

    public TextMeasureResult(
        int estimatedWidth,
        int estimatedHeight,
        int lineCount,
        string displayText,
        bool isTruncated,
        string fullText,
        IReadOnlyList<TextMeasureLineResult> lines)
    {
        EstimatedWidth = estimatedWidth;
        EstimatedHeight = estimatedHeight;
        LineCount = lineCount;
        DisplayText = displayText;
        IsTruncated = isTruncated;
        FullText = fullText;
        Lines = lines;
    }

    public int EstimatedWidth { get; }

    public int EstimatedHeight { get; }

    public int LineCount { get; }

    public string DisplayText { get; }

    public bool IsTruncated { get; }

    public string FullText { get; }

    public IReadOnlyList<TextMeasureLineResult> Lines { get; }

    private static IReadOnlyList<TextMeasureLineResult> BuildFallbackLines(
        string displayText,
        int estimatedWidth,
        bool isTruncated)
    {
        var lines = (displayText ?? string.Empty)
            .Split(Environment.NewLine, StringSplitOptions.None)
            .Where(line => line.Length > 0)
            .Select((line, index) => new TextMeasureLineResult(index, line, estimatedWidth, isTruncated && index == 0))
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add(new TextMeasureLineResult(0, string.Empty, 0, false));
        }

        return lines;
    }
}

public sealed class TextMeasurePreviewSample
{
    public string Label { get; init; } = string.Empty;

    public string ConstraintLabel { get; init; } = string.Empty;

    public string FontLabel { get; init; } = string.Empty;

    public string FullText { get; init; } = string.Empty;

    public TextMeasureResult Result { get; init; } = new(0, 0, 1, string.Empty, false);
}

public sealed class TextMeasurePreviewSnapshot
{
    public string TestHookId { get; init; } = "text-measure-service-preview";

    public string Label { get; init; } = "Text measure service";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<TextMeasurePreviewSample> Samples { get; init; } = [];
}

public static class TextMeasurePreviewFactory
{
    public static TextMeasurePreviewSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var service = new TextMeasureService();
        var titleFont = new TextMeasureFontSpec
        {
            Family = "\"DM Sans\", sans-serif",
            SizePx = 16,
            Weight = 700,
            LineHeightPx = 19
        };
        var eventFont = new TextMeasureFontSpec
        {
            Family = "\"DM Sans\", sans-serif",
            SizePx = 12,
            Weight = 700,
            LineHeightPx = 15
        };
        var chipFont = new TextMeasureFontSpec
        {
            Family = "\"DM Sans\", sans-serif",
            SizePx = 11,
            Weight = 700,
            LineHeightPx = 12
        };

        var samples = new List<TextMeasurePreviewSample>
        {
            CreateSample(
                service,
                "Node title",
                ResolveLongestNodeTitle(surface),
                184,
                2,
                titleFont),
            CreateSample(
                service,
                "Calendar tile",
                ResolveCalendarLikeTitle(surface),
                152,
                2,
                eventFont),
            CreateSample(
                service,
                "Chip badge",
                ResolveLongestChip(surface),
                132,
                1,
                chipFont)
        };

        var truncatedCount = samples.Count(sample => sample.Result.IsTruncated);

        return new TextMeasurePreviewSnapshot
        {
            Title = "Shared text fitting now owns truncation and line-clamp rules",
            Summary = "Workbench node estimates, radial menu labels, and calendar tiles now route through the same text measurement seam instead of keeping separate fit loops.",
            StatePill = truncatedCount > 0 ? "Ready" : "Stable",
            Metrics =
            [
                "Graph + calendar",
                "Canvas + DOM",
                $"{samples.Count} preview samples",
                $"{truncatedCount} tooltip fallbacks"
            ],
            Samples = samples
        };
    }

    private static TextMeasurePreviewSample CreateSample(
        TextMeasureService service,
        string label,
        string fullText,
        double maxWidth,
        int maxLines,
        TextMeasureFontSpec font)
    {
        var result = service.Measure(new TextMeasureRequest
        {
            Id = label.Replace(' ', '-').ToLowerInvariant(),
            Text = fullText,
            Font = font,
            MaxWidth = maxWidth,
            MaxLines = maxLines
        });

        return new TextMeasurePreviewSample
        {
            Label = label,
            ConstraintLabel = $"{Math.Round(maxWidth)} px / {maxLines} line{(maxLines == 1 ? string.Empty : "s")}",
            FontLabel = $"{font.Weight} / {font.SizePx:0.#} px",
            FullText = fullText,
            Result = result
        };
    }

    private static string ResolveLongestNodeTitle(CanvasWorkbenchSurface surface)
        => surface.Nodes
            .Select(node => node.IsInlineTextNode ? node.InlineText : node.Title)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderByDescending(value => value!.Length)
            .FirstOrDefault()
        ?? "North star delivery plan with approvals and external dependencies";

    private static string ResolveCalendarLikeTitle(CanvasWorkbenchSurface surface)
        => surface.Nodes
            .Select(node => string.Join(" ", new[] { node.Title, node.Subtitle, node.LeadText }.Where(value => !string.IsNullOrWhiteSpace(value))))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderByDescending(value => value!.Length)
            .FirstOrDefault()
        ?? "Client planning sync with long-running logistics, playlist handoff, and venue notes";

    private static string ResolveLongestChip(CanvasWorkbenchSurface surface)
        => surface.Nodes
            .SelectMany(
                node => node.Chips
                    .Concat(node.FooterChips)
                    .Select(chip => chip.Text)
                    .Concat(node.Annotations.Select(annotation => annotation.Label)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderByDescending(value => value!.Length)
            .FirstOrDefault()
        ?? "Validation required before Friday handoff";
}

public sealed class TextMeasureService
{
    private const double DefaultCharacterWidth = 8;
    private const double DefaultLineHeight = 20;

    public TextMeasureResult Measure(string? text, int maxLineLength, int maxLines = 1)
    {
        var normalized = NormalizeText(text);
        if (normalized.Length == 0)
        {
            return new TextMeasureResult(0, (int)DefaultLineHeight, 1, string.Empty, false);
        }

        return Measure(new TextMeasureRequest
        {
            Text = normalized,
            MaxWidth = Math.Max(1, maxLineLength) * DefaultCharacterWidth,
            MaxLines = maxLines,
            Font = new TextMeasureFontSpec
            {
                SizePx = 12,
                Weight = 600,
                LineHeightPx = DefaultLineHeight
            }
        });
    }

    public TextMeasureResult Measure(TextMeasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = NormalizeText(request.Text);
        var font = request.Font ?? TextMeasureFontSpec.Default;
        var maxWidth = Math.Max(24, request.MaxWidth);
        var maxLines = Math.Max(1, request.MaxLines);
        var lineHeight = EstimateLineHeight(font);

        if (normalized.Length == 0)
        {
            return new TextMeasureResult(0, (int)Math.Ceiling(lineHeight), 1, string.Empty, false);
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var lineResults = new List<TextMeasureLineResult>();
        var currentLine = string.Empty;
        var tokenIndex = 0;
        var truncated = false;

        for (; tokenIndex < tokens.Length; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            var candidate = currentLine.Length == 0
                ? token
                : $"{currentLine} {token}";

            if (MeasureWidth(candidate, font) <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (currentLine.Length > 0)
            {
                AppendMeasuredLine(lines, lineResults, currentLine, font, false);
                if (lines.Count >= maxLines)
                {
                    truncated = true;
                    break;
                }
            }

            if (MeasureWidth(token, font) <= maxWidth)
            {
                currentLine = token;
                continue;
            }

            currentLine = FitTextToWidth(token, maxWidth, font, request.TruncationMode);
            truncated = !string.Equals(currentLine, token, StringComparison.Ordinal);
            AppendMeasuredLine(lines, lineResults, currentLine, font, truncated);
            currentLine = string.Empty;

            if (lines.Count >= maxLines)
            {
                truncated = truncated || tokenIndex < tokens.Length - 1;
                break;
            }
        }

        if (currentLine.Length > 0 && lines.Count < maxLines)
        {
            AppendMeasuredLine(lines, lineResults, currentLine, font, false);
        }

        if (lines.Count == 0)
        {
            var fitted = FitTextToWidth(normalized, maxWidth, font, request.TruncationMode);
            truncated = !string.Equals(fitted, normalized, StringComparison.Ordinal);
            AppendMeasuredLine(lines, lineResults, fitted, font, truncated);
        }

        if (tokenIndex < tokens.Length - 1)
        {
            truncated = true;
        }

        if (truncated && lines.Count > 0)
        {
            var lastLine = EnsureEllipsis(lines[^1], maxWidth, font, request.TruncationMode);
            lines[^1] = lastLine;
            lineResults[^1] = lineResults[^1] with
            {
                Text = lastLine,
                EstimatedWidth = (int)Math.Ceiling(MeasureWidth(lastLine, font)),
                IsEllipsized = true
            };
        }

        var widestLine = lineResults.Count == 0
            ? 0
            : lineResults.Max(line => line.EstimatedWidth);

        return new TextMeasureResult(
            widestLine,
            (int)Math.Ceiling(lineResults.Count * lineHeight),
            lineResults.Count,
            string.Join(Environment.NewLine, lines),
            truncated,
            normalized,
            lineResults);
    }

    public string FitWithEllipsis(string? text, int maxLength)
    {
        var normalized = NormalizeText(text);
        if (normalized.Length <= Math.Max(1, maxLength))
        {
            return normalized;
        }

        const string suffix = "...";
        var allowed = Math.Max(1, maxLength - suffix.Length);
        var fitted = string.Concat(EnumerateTextElements(normalized).Take(allowed)).TrimEnd();
        return fitted.Length == 0 ? suffix : $"{fitted}{suffix}";
    }

    private static void AppendMeasuredLine(
        List<string> lines,
        List<TextMeasureLineResult> results,
        string line,
        TextMeasureFontSpec font,
        bool isEllipsized)
    {
        lines.Add(line);
        results.Add(new TextMeasureLineResult(
            results.Count,
            line,
            (int)Math.Ceiling(MeasureWidth(line, font)),
            isEllipsized));
    }

    private static string EnsureEllipsis(
        string line,
        double maxWidth,
        TextMeasureFontSpec font,
        string truncationMode)
    {
        if (!UseEllipsis(truncationMode) || line.EndsWith("...", StringComparison.Ordinal))
        {
            return line;
        }

        const string suffix = "...";
        var candidate = $"{line.TrimEnd()}{suffix}";
        if (MeasureWidth(candidate, font) <= maxWidth)
        {
            return candidate;
        }

        return FitTextToWidth(candidate, maxWidth, font, truncationMode);
    }

    private static string FitTextToWidth(
        string text,
        double maxWidth,
        TextMeasureFontSpec font,
        string truncationMode)
    {
        var normalized = NormalizeText(text);
        if (normalized.Length == 0 || MeasureWidth(normalized, font) <= maxWidth)
        {
            return normalized;
        }

        if (!UseEllipsis(truncationMode))
        {
            return normalized;
        }

        const string suffix = "...";
        var suffixWidth = MeasureWidth(suffix, font);
        if (suffixWidth >= maxWidth)
        {
            return suffix;
        }

        var buffer = string.Empty;
        foreach (var element in EnumerateTextElements(normalized))
        {
            var candidate = buffer + element;
            if (MeasureWidth(candidate, font) + suffixWidth > maxWidth)
            {
                break;
            }

            buffer = candidate;
        }

        buffer = buffer.TrimEnd();
        return buffer.Length == 0
            ? suffix
            : $"{buffer}{suffix}";
    }

    private static double MeasureWidth(string text, TextMeasureFontSpec font)
    {
        var value = NormalizeText(text);
        if (value.Length == 0)
        {
            return 0;
        }

        var width = 0d;
        var elements = EnumerateTextElements(value).ToList();
        for (var index = 0; index < elements.Count; index++)
        {
            width += font.SizePx * ResolveWidthFactor(elements[index]);
            if (index < elements.Count - 1)
            {
                width += font.LetterSpacingPx;
            }
        }

        return width;
    }

    private static double EstimateLineHeight(TextMeasureFontSpec font)
        => font.LineHeightPx > 0
            ? font.LineHeightPx
            : Math.Max(DefaultLineHeight, font.SizePx * 1.35);

    private static IEnumerable<string> EnumerateTextElements(string value)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            yield return (string)enumerator.Current!;
        }
    }

    private static double ResolveWidthFactor(string textElement)
    {
        if (string.IsNullOrEmpty(textElement))
        {
            return 0;
        }

        if (textElement.Length > 1)
        {
            return 0.96;
        }

        var ch = textElement[0];
        if (char.IsWhiteSpace(ch))
        {
            return 0.34;
        }

        if (char.IsDigit(ch))
        {
            return 0.54;
        }

        if (char.IsUpper(ch))
        {
            return 0.64;
        }

        if (char.IsLower(ch))
        {
            return 0.56;
        }

        if (char.IsPunctuation(ch) || char.IsSymbol(ch))
        {
            return 0.4;
        }

        return 0.6;
    }

    private static bool UseEllipsis(string truncationMode)
        => !string.Equals(truncationMode, "clip", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeText(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}


