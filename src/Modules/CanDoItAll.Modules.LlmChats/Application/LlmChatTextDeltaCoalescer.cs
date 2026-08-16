using System.Text;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatTextDeltaCoalescer
{
    private readonly LlmChatStreamingOptions _options;
    private readonly StringBuilder _buffer = new();
    private DateTimeOffset? _bufferedAtUtc;

    public LlmChatTextDeltaCoalescer(LlmChatStreamingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    public IReadOnlyList<string> Append(string delta, DateTimeOffset observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(delta);
        _bufferedAtUtc ??= observedAtUtc;
        _buffer.Append(delta);
        var bytes = Encoding.UTF8.GetByteCount(_buffer.ToString());
        var elapsed = observedAtUtc - _bufferedAtUtc.Value;
        if (bytes < _options.MinimumChunkBytes &&
            elapsed < _options.MaximumCoalescingDelay &&
            !EndsAtNaturalBoundary(_buffer))
        {
            return [];
        }

        return Flush(observedAtUtc);
    }

    public TimeSpan GetRemainingDelay(DateTimeOffset observedAtUtc)
    {
        if (_bufferedAtUtc is null)
        {
            throw new InvalidOperationException("The text delta coalescer has no buffered content.");
        }

        var remaining = _options.MaximumCoalescingDelay - (observedAtUtc - _bufferedAtUtc.Value);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public bool HasBufferedContent => _buffer.Length > 0;

    public int BufferedChunkCount => _buffer.Length == 0
        ? 0
        : SplitUtf8Bounded(_buffer.ToString(), _options.MaximumChunkBytes).Count;

    public IReadOnlyList<string> Flush(DateTimeOffset observedAtUtc)
    {
        if (_buffer.Length == 0)
        {
            return [];
        }

        var chunks = SplitUtf8Bounded(_buffer.ToString(), _options.MaximumChunkBytes);
        _buffer.Clear();
        _bufferedAtUtc = null;
        return chunks;
    }

    private static bool EndsAtNaturalBoundary(StringBuilder buffer)
        => buffer[^1] is '\n' or '\r' or '.' or '!' or '?';

    private static IReadOnlyList<string> SplitUtf8Bounded(string text, int maximumBytes)
    {
        var chunks = new List<string>();
        var chunk = new StringBuilder();
        var chunkBytes = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            if (chunkBytes > 0 && chunkBytes + runeBytes > maximumBytes)
            {
                chunks.Add(chunk.ToString());
                chunk.Clear();
                chunkBytes = 0;
            }

            chunk.Append(rune.ToString());
            chunkBytes += runeBytes;
        }

        if (chunk.Length > 0)
        {
            chunks.Add(chunk.ToString());
        }

        return chunks;
    }
}
