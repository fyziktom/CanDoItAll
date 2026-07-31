namespace CanDoItAll.Modules.Processes;

internal static class ProcessToolReceiptRequestArgumentReader
{
    public static bool TryReadString(
        string requestSummary,
        string argumentName,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(requestSummary) || string.IsNullOrWhiteSpace(argumentName))
        {
            return false;
        }

        var arguments = RemoveToolSignaturePrefix(requestSummary.Trim());
        var foundNamedArgument = false;
        foreach (var segment in EnumerateTopLevelSegments(arguments))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            foundNamedArgument = true;
            var name = segment[..separatorIndex].Trim();
            if (!string.Equals(name, argumentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return TryNormalizeValue(segment[(separatorIndex + 1)..], out value);
        }

        if (foundNamedArgument)
        {
            return false;
        }

        return TryNormalizeValue(arguments, out value);
    }

    private static string RemoveToolSignaturePrefix(string requestSummary)
    {
        var separatorIndex = requestSummary.IndexOf('|');
        var assignmentIndex = requestSummary.IndexOf('=');
        return separatorIndex >= 0 &&
               (assignmentIndex < 0 || separatorIndex < assignmentIndex)
            ? requestSummary[(separatorIndex + 1)..]
            : requestSummary;
    }

    private static IEnumerable<string> EnumerateTopLevelSegments(string arguments)
    {
        var segmentStart = 0;
        var quote = '\0';
        var escaped = false;
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var index = 0; index < arguments.Length; index++)
        {
            var character = arguments[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
                continue;
            }

            switch (character)
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth = Math.Max(0, braceDepth - 1);
                    break;
                case ',' when parenthesisDepth == 0 &&
                                   bracketDepth == 0 &&
                                   braceDepth == 0:
                    yield return arguments[segmentStart..index].Trim();
                    segmentStart = index + 1;
                    break;
            }
        }

        if (segmentStart <= arguments.Length)
        {
            yield return arguments[segmentStart..].Trim();
        }
    }

    private static bool TryNormalizeValue(string rawValue, out string value)
    {
        value = rawValue.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        if (value[0] is not ('"' or '\''))
        {
            return true;
        }

        var quote = value[0];
        if (value.Length < 2 || value[^1] != quote)
        {
            value = string.Empty;
            return false;
        }

        value = value[1..^1].Trim();
        return value.Length > 0;
    }
}
