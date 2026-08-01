namespace CanDoItAll.Modules.Processes;

using CanDoItAll.AgentFramework.Core;

internal readonly record struct ParentSubprocessRuntimeEnvelopeSpan(
    int Start,
    int Length);

internal static class ParentSubprocessRuntimeEnvelopeBudget
{
    internal const int ManagedArtifactHeadroomCharacters = 16_000;
    internal const int MaxCombinedEnvelopeCharacters =
        WorkspaceFileLimits.MaxTextReadCharacters - ManagedArtifactHeadroomCharacters;

    internal static bool IsWithinLimit(
        ProcessSubprocessVerifiedChildArtifact childOutput,
        IReadOnlyList<ParentSubprocessForwardedContextArtifact> forwardedContextArtifacts,
        out int combinedCharacters)
    {
        combinedCharacters = ParentSubprocessVerifiedChildOutputEnvelope.Format(childOutput).Length;
        if (forwardedContextArtifacts.Count > 0)
        {
            combinedCharacters += ParentSubprocessForwardedContextEnvelope
                .Format(forwardedContextArtifacts)
                .Length;
        }

        return combinedCharacters <= MaxCombinedEnvelopeCharacters;
    }
}

internal static class ParentSubprocessRuntimeEnvelopeFraming
{
    internal static IEnumerable<string> EnumerateLinesOutsideCodeFences(string content)
    {
        CodeFence? activeFence = null;
        var lineStart = 0;
        while (lineStart <= content.Length)
        {
            var newLineIndex = content.IndexOf('\n', lineStart);
            var lineEnd = newLineIndex < 0 ? content.Length : newLineIndex;
            var contentEnd = lineEnd > lineStart && content[lineEnd - 1] == '\r'
                ? lineEnd - 1
                : lineEnd;
            var line = content[lineStart..contentEnd];
            if (activeFence is not null)
            {
                if (IsClosingFence(line, activeFence.Value))
                {
                    activeFence = null;
                }
            }
            else if (TryReadOpeningFence(line, out var openingFence))
            {
                activeFence = openingFence;
            }
            else
            {
                yield return line;
            }

            if (newLineIndex < 0)
            {
                yield break;
            }

            lineStart = newLineIndex + 1;
        }
    }

    internal static bool TryFindTopLevelSpans(
        string content,
        string beginMarker,
        string endMarker,
        out IReadOnlyList<ParentSubprocessRuntimeEnvelopeSpan> spans)
    {
        var resolvedSpans = new List<ParentSubprocessRuntimeEnvelopeSpan>();
        int? envelopeStart = null;
        CodeFence? activeFence = null;
        var lineStart = 0;
        while (lineStart <= content.Length)
        {
            var newLineIndex = content.IndexOf('\n', lineStart);
            var lineEnd = newLineIndex < 0 ? content.Length : newLineIndex;
            var contentEnd = lineEnd > lineStart && content[lineEnd - 1] == '\r'
                ? lineEnd - 1
                : lineEnd;
            var line = content[lineStart..contentEnd];

            if (activeFence is not null)
            {
                if (IsClosingFence(line, activeFence.Value))
                {
                    activeFence = null;
                }
            }
            else if (TryReadOpeningFence(line, out var openingFence))
            {
                activeFence = openingFence;
            }
            else
            {
                if (string.Equals(line, beginMarker, StringComparison.Ordinal))
                {
                    if (envelopeStart is not null)
                    {
                        spans = [];
                        return false;
                    }

                    envelopeStart = lineStart;
                }
                else if (string.Equals(line, endMarker, StringComparison.Ordinal))
                {
                    if (envelopeStart is null)
                    {
                        spans = [];
                        return false;
                    }

                    resolvedSpans.Add(new ParentSubprocessRuntimeEnvelopeSpan(
                        envelopeStart.Value,
                        contentEnd - envelopeStart.Value));
                    envelopeStart = null;
                }
            }

            if (newLineIndex < 0)
            {
                break;
            }

            lineStart = newLineIndex + 1;
        }

        if (envelopeStart is not null)
        {
            spans = [];
            return false;
        }

        spans = resolvedSpans;
        return true;
    }

    internal static bool TryRemoveSingleVerified(
        string content,
        string? verifiedEnvelope,
        string beginMarker,
        string endMarker,
        out string contentWithoutEnvelope,
        out bool wasPresent)
    {
        contentWithoutEnvelope = content;
        wasPresent = false;
        if (!TryFindTopLevelSpans(
                content,
                beginMarker,
                endMarker,
                out var spans))
        {
            return false;
        }

        if (spans.Count == 0)
        {
            return string.IsNullOrWhiteSpace(verifiedEnvelope);
        }

        wasPresent = true;
        if (spans.Count != 1 || string.IsNullOrWhiteSpace(verifiedEnvelope))
        {
            return false;
        }

        var span = spans[0];
        var candidateEnvelope = content.Substring(span.Start, span.Length);
        if (!string.Equals(candidateEnvelope, verifiedEnvelope, StringComparison.Ordinal))
        {
            return false;
        }

        contentWithoutEnvelope = content.Remove(span.Start, span.Length);
        return true;
    }

    private static bool TryReadOpeningFence(string line, out CodeFence fence)
    {
        if (!TryRemoveCommonMarkFenceIndentation(line, out var candidate) ||
            candidate.Length < 3 ||
            candidate[0] is not ('`' or '~'))
        {
            fence = default;
            return false;
        }

        var marker = candidate[0];
        var markerLength = 1;
        while (markerLength < candidate.Length && candidate[markerLength] == marker)
        {
            markerLength++;
        }

        if (markerLength < 3 ||
            marker == '`' && candidate[markerLength..].Contains('`'))
        {
            fence = default;
            return false;
        }

        fence = new CodeFence(marker, markerLength);
        return true;
    }

    private static bool IsClosingFence(string line, CodeFence openingFence)
    {
        if (!TryRemoveCommonMarkFenceIndentation(line, out var candidate))
        {
            return false;
        }

        var markerLength = 0;
        while (markerLength < candidate.Length &&
               candidate[markerLength] == openingFence.Marker)
        {
            markerLength++;
        }

        return markerLength >= openingFence.Length &&
               markerLength == candidate.Length;
    }

    private static bool TryRemoveCommonMarkFenceIndentation(
        string line,
        out ReadOnlySpan<char> candidate)
    {
        var indentation = 0;
        while (indentation < line.Length && line[indentation] == ' ')
        {
            indentation++;
        }

        if (indentation > 3)
        {
            candidate = default;
            return false;
        }

        candidate = line.AsSpan(indentation);
        while (!candidate.IsEmpty && candidate[^1] is ' ' or '\t')
        {
            candidate = candidate[..^1];
        }

        return true;
    }

    private readonly record struct CodeFence(char Marker, int Length);
}
