namespace CanDoItAll.Components.Mermaid;

public static class MermaidSourceNormalizer
{
    private static readonly string[] DiagramPrefixes =
    [
        "architecture-beta",
        "block-beta",
        "c4component",
        "c4container",
        "c4context",
        "c4deployment",
        "c4dynamic",
        "classdiagram",
        "erdiagram",
        "flowchart",
        "gantt",
        "gitgraph",
        "graph",
        "journey",
        "kanban",
        "mindmap",
        "packet-beta",
        "pie",
        "quadrantchart",
        "radar-beta",
        "requirementdiagram",
        "sankey-beta",
        "sequencediagram",
        "statediagram",
        "timeline",
        "treemap-beta",
        "xychart-beta"
    ];

    public static string Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var text = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim('\uFEFF')
            .Trim();

        return TryExtractFencedSource(text, out var fencedSource)
            ? fencedSource
            : text;
    }

    private static bool TryExtractFencedSource(string text, out string source)
    {
        source = string.Empty;

        if (TryExtractSingleLineFencedSource(text, out source))
        {
            return true;
        }

        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryReadOpeningFence(lines[index], out var fenceCharacter, out var fenceLength, out var info))
            {
                continue;
            }

            var hasMermaidInfo = TryReadMermaidInfo(info, out var infoRemainder);
            for (var closingIndex = index + 1; closingIndex < lines.Length; closingIndex++)
            {
                if (!IsClosingFence(lines[closingIndex], fenceCharacter, fenceLength))
                {
                    continue;
                }

                var body = string.Join('\n', lines.Skip(index + 1).Take(closingIndex - index - 1)).Trim();
                if (hasMermaidInfo)
                {
                    source = LooksLikeMermaidSource(infoRemainder)
                        ? JoinSourceLines(infoRemainder, body)
                        : body;
                    return !string.IsNullOrWhiteSpace(source);
                }

                if (LooksLikeMermaidSource(body))
                {
                    source = body;
                    return true;
                }

                break;
            }

            if (hasMermaidInfo)
            {
                var body = string.Join('\n', lines.Skip(index + 1)).Trim();
                if (LooksLikeMermaidSource(infoRemainder))
                {
                    source = JoinSourceLines(infoRemainder, body);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(body))
                {
                    source = body;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryExtractSingleLineFencedSource(string text, out string source)
    {
        source = string.Empty;
        if (text.Contains('\n') ||
            !TryReadOpeningFence(text, out var fenceCharacter, out var fenceLength, out var info))
        {
            return false;
        }

        if (!TryReadMermaidInfo(info, out var remainder))
        {
            return false;
        }

        var closingFence = new string(fenceCharacter, fenceLength);
        if (!remainder.EndsWith(closingFence, StringComparison.Ordinal))
        {
            return false;
        }

        source = remainder[..^closingFence.Length].Trim();
        return LooksLikeMermaidSource(source);
    }

    private static bool TryReadOpeningFence(
        string line,
        out char fenceCharacter,
        out int fenceLength,
        out string info)
    {
        fenceCharacter = '\0';
        fenceLength = 0;
        info = string.Empty;

        var trimmed = line.TrimStart();
        if (trimmed.Length < 3 || trimmed[0] is not ('`' or '~'))
        {
            return false;
        }

        fenceCharacter = trimmed[0];
        while (fenceLength < trimmed.Length && trimmed[fenceLength] == fenceCharacter)
        {
            fenceLength++;
        }

        if (fenceLength < 3)
        {
            return false;
        }

        info = trimmed[fenceLength..].Trim();
        return true;
    }

    private static bool IsClosingFence(string line, char fenceCharacter, int fenceLength)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= fenceLength &&
               trimmed.All(character => character == fenceCharacter);
    }

    private static bool TryReadMermaidInfo(string info, out string remainder)
    {
        remainder = string.Empty;
        if (string.IsNullOrWhiteSpace(info))
        {
            return false;
        }

        var trimmed = info.Trim();
        var firstWhitespace = trimmed.IndexOfAny([' ', '\t']);
        var language = firstWhitespace < 0 ? trimmed : trimmed[..firstWhitespace];
        if (!language.Equals("mermaid", StringComparison.OrdinalIgnoreCase) &&
            !language.Equals("mmd", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        remainder = firstWhitespace < 0 ? string.Empty : trimmed[(firstWhitespace + 1)..].Trim();
        return true;
    }

    private static string JoinSourceLines(params string[] values)
        => string.Join('\n', values.Select(value => value.Trim()).Where(value => value.Length > 0));

    private static bool LooksLikeMermaidSource(string source)
    {
        var firstLine = source
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 && !line.StartsWith("%%", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return false;
        }

        var normalized = firstLine.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return DiagramPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
    }
}
