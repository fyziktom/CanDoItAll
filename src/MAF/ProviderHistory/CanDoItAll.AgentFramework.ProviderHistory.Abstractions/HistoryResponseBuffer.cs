using System.Text;

namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed class HistoryResponseBuffer {
    private readonly StringBuilder? text;
    private readonly int maximumCharacters;
    private long originalBytes;
    private bool pendingHighSurrogate;
    public long OriginalBytes => checked(originalBytes + (pendingHighSurrogate ? 3 : 0));

    public HistoryResponseBuffer(HistoryAttemptStart start) {
        if (start.ContentOwner is null && start.Policy.Policy.CaptureMode == HistoryCaptureMode.Detailed) {
            maximumCharacters = start.Policy.Policy.MaximumTextBytes + 128 * 1024 + 1024;
            text = new();
        }
    }

    public void Append(string value) {
        if (text is null) {
            return;
        }
        originalBytes = checked(originalBytes + CountBytes(value));
        var count = Math.Min(value.Length, maximumCharacters - text.Length);
        if (count > 0) {
            text.Append(value.AsSpan(0, count));
        }
    }

    private long CountBytes(ReadOnlySpan<char> value) {
        if (value.IsEmpty) {
            return 0;
        }
        var bytes = 0L;
        if (pendingHighSurrogate) {
            var paired = char.IsLowSurrogate(value[0]);
            bytes = paired ? 4 : 3;
            if (paired) {
                value = value[1..];
            }
        }
        pendingHighSurrogate = value.Length > 0 && char.IsHighSurrogate(value[^1]);
        if (pendingHighSurrogate) {
            value = value[..^1];
        }
        return bytes + Encoding.UTF8.GetByteCount(value);
    }

    public string? GetText() {
        if (text is null) {
            return null;
        }
        var length = text.Length;
        if (length > 0 && char.IsHighSurrogate(text[length - 1])) {
            length--;
        }
        return text.ToString(0, length);
    }
}
