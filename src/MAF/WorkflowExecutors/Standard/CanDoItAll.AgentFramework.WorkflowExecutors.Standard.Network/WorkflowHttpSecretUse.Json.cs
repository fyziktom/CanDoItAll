using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

public sealed partial class WorkflowHttpSecretUse {
    private string RedactJsonStrings(string value, bool truncated) {
        var trimmed = value.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] is not ('{' or '[' or '"')) {
            return value;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        var reader = new Utf8JsonReader(bytes, isFinalBlock: !truncated,
            new JsonReaderState(new JsonReaderOptions { MaxDepth = int.MaxValue }));
        List<(int Start, int End, byte[] Replacement)>? edits = null;
        try {
            while (reader.Read()) {
                if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.PropertyName)) {
                    continue;
                }
                string decoded;
                try {
                    decoded = reader.GetString()!;
                } catch (InvalidOperationException) {
                    var start = checked((int)reader.TokenStartIndex);
                    (edits ??= []).Add((start, start + reader.ValueSpan.Length + 2, "\"[REDACTED]\""u8.ToArray()));
                    continue;
                }
                var safe = Redact(decoded);
                if (!string.Equals(decoded, safe, StringComparison.Ordinal)) {
                    var start = checked((int)reader.TokenStartIndex);
                    (edits ??= []).Add((start, start + reader.ValueSpan.Length + 2, JsonSerializer.SerializeToUtf8Bytes(safe)));
                }
            }
            if (truncated) {
                var start = checked((int)reader.BytesConsumed);
                while (start < bytes.Length && bytes[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') {
                    start++;
                }
                if (start < bytes.Length && bytes[start] == (byte)',') {
                    start++;
                    while (start < bytes.Length && bytes[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') {
                        start++;
                    }
                }
                if (start < bytes.Length && bytes[start] == (byte)'"') {
                    (edits ??= []).Add((start, bytes.Length, "\"[REDACTED]\""u8.ToArray()));
                }
            }
        } catch (JsonException) {
            // Completed string tokens can still be sanitized when the remaining body is not JSON.
        }
        if (edits is null) {
            return value;
        }
        using var output = new MemoryStream();
        var position = 0;
        foreach (var edit in edits) {
            output.Write(bytes.AsSpan(position, edit.Start - position));
            output.Write(edit.Replacement);
            position = edit.End;
        }
        output.Write(bytes.AsSpan(position));
        return Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length));
    }
}
