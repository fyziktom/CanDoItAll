using System.Text;

namespace CanDoItAll.AgentFramework.Voice;

public static class AgentVoiceSpeechTextChunker
{
    public const int DefaultMaxChunkCharacters = 600;
    public const int MinimumMaxChunkCharacters = 80;

    public static IReadOnlyList<string> Split(
        string text,
        int maxChunkCharacters = DefaultMaxChunkCharacters)
    {
        if (maxChunkCharacters < MinimumMaxChunkCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxChunkCharacters),
                maxChunkCharacters,
                $"Speech chunks must allow at least {MinimumMaxChunkCharacters} characters.");
        }

        var normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var sentences = SplitIntoSentenceLikeSegments(normalized);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (sentence.Length > maxChunkCharacters)
            {
                FlushCurrent();
                chunks.AddRange(SplitLongSegment(sentence, maxChunkCharacters));
                continue;
            }

            if (current.Length == 0)
            {
                current.Append(sentence);
                continue;
            }

            if (current.Length + 1 + sentence.Length <= maxChunkCharacters)
            {
                current.Append(' ');
                current.Append(sentence);
                continue;
            }

            FlushCurrent();
            current.Append(sentence);
        }

        FlushCurrent();
        return chunks;

        void FlushCurrent()
        {
            if (current.Length == 0)
            {
                return;
            }

            chunks.Add(current.ToString());
            current.Clear();
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(
            ' ',
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IReadOnlyList<string> SplitIntoSentenceLikeSegments(string text)
    {
        var segments = new List<string>();
        var startIndex = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (!IsSentenceBoundary(text[index]) || !IsBoundaryAtSegmentEnd(text, index))
            {
                continue;
            }

            AddSegment(index + 1);
            startIndex = index + 1;
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
                index = startIndex - 1;
            }
        }

        AddSegment(text.Length);
        return segments.Count == 0 ? [text] : segments;

        void AddSegment(int endIndex)
        {
            if (endIndex <= startIndex)
            {
                return;
            }

            var segment = text[startIndex..endIndex].Trim();
            if (!string.IsNullOrWhiteSpace(segment))
            {
                segments.Add(segment);
            }
        }
    }

    private static bool IsSentenceBoundary(char character)
    {
        return character is '.' or '!' or '?' or ';';
    }

    private static bool IsBoundaryAtSegmentEnd(string text, int boundaryIndex)
    {
        if (boundaryIndex == text.Length - 1)
        {
            return true;
        }

        var nextIndex = boundaryIndex + 1;
        while (nextIndex < text.Length && IsClosingPunctuation(text[nextIndex]))
        {
            nextIndex++;
        }

        return nextIndex >= text.Length || char.IsWhiteSpace(text[nextIndex]);
    }

    private static bool IsClosingPunctuation(char character)
    {
        return character is '"' or '\'' or ')' or ']' or '}';
    }

    private static IEnumerable<string> SplitLongSegment(
        string segment,
        int maxChunkCharacters)
    {
        var remaining = segment.Trim();
        while (remaining.Length > maxChunkCharacters)
        {
            var splitIndex = remaining.LastIndexOf(' ', maxChunkCharacters);
            if (splitIndex < MinimumMaxChunkCharacters)
            {
                splitIndex = maxChunkCharacters;
            }

            yield return remaining[..splitIndex].Trim();
            remaining = remaining[splitIndex..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            yield return remaining;
        }
    }
}
