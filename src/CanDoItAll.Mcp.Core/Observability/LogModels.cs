using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.Mcp.Core.Observability;

public record LogEntry(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Source,
    string? Stream,
    int? SessionVersion,
    string CorrelationId,
    string Text);

public record LogReadResult(
    IReadOnlyList<LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor);

public class RingLogBuffer
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly LinkedList<LogEntry> _entries = [];
    private long _lastSequence;

    public RingLogBuffer(int capacity)
    {
        _capacity = Math.Max(50, capacity);
    }

    public long CurrentSequence
    {
        get
        {
            lock (_gate)
            {
                return _lastSequence;
            }
        }
    }

    public LogEntry Append(string source, string? stream, int? sessionVersion, string correlationId, string text)
    {
        lock (_gate)
        {
            var entry = new LogEntry(++_lastSequence, DateTimeOffset.UtcNow, source, stream, sessionVersion, correlationId, text);
            _entries.AddLast(entry);
            while (_entries.Count > _capacity)
            {
                _entries.RemoveFirst();
            }

            return entry;
        }
    }

    public LogReadResult ReadAfter(long? cursor, int limit, Func<LogEntry, bool>? predicate = null)
    {
        lock (_gate)
        {
            var startCursor = cursor ?? 0;
            var allEntries = _entries
                .Where(entry => entry.Sequence > startCursor && (predicate?.Invoke(entry) ?? true))
                .ToList();
            var take = Math.Clamp(limit, 1, 500);
            var selected = allEntries.Take(take).ToList();
            var nextCursor = selected.Count == 0 ? startCursor : selected[^1].Sequence;
            return new LogReadResult(selected, nextCursor, allEntries.Count > selected.Count, Math.Max(0, allEntries.Count - selected.Count));
        }
    }

    public IReadOnlyList<LogEntry> GetLatest(int limit)
    {
        lock (_gate)
        {
            return _entries.TakeLast(Math.Clamp(limit, 1, 500)).ToList();
        }
    }

    public IReadOnlyList<LogEntry> GetAfter(long cursor)
    {
        lock (_gate)
        {
            return _entries.Where(entry => entry.Sequence > cursor).ToList();
        }
    }
}

public sealed record FileLogStoreOptions
{
    public bool Enabled { get; init; } = true;

    public string RootDirectory { get; init; } = ".";

    public long MaxFileSizeBytes { get; init; } = 50L * 1024L * 1024L;
}

public class FileLogStore
{
    private readonly FileLogStoreOptions _options;
    private readonly ConcurrentDictionary<string, object> _fileLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public FileLogStore(FileLogStoreOptions options)
    {
        _options = options;
        Directory.CreateDirectory(_options.RootDirectory);
    }

    public void Append(string ownerKind, string ownerId, LogEntry entry)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var path = Path.Combine(_options.RootDirectory, $"{ownerKind.ToLowerInvariant()}-{Sanitize(ownerId)}.ndjson");
        var fileLock = _fileLocks.GetOrAdd(path, static _ => new object());

        lock (fileLock)
        {
            RotateIfNeeded(path);
            File.AppendAllText(path, JsonSerializer.Serialize(entry, _serializerOptions) + Environment.NewLine);
        }
    }

    private void RotateIfNeeded(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length < _options.MaxFileSizeBytes)
        {
            return;
        }

        var rotatedPath = $"{path}.1";
        if (File.Exists(rotatedPath))
        {
            File.Delete(rotatedPath);
        }

        File.Move(path, rotatedPath, overwrite: true);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }
}

public sealed record SecretRedactionOptions
{
    public bool Enabled { get; init; } = true;

    public string Replacement { get; init; } = "***redacted***";

    public IReadOnlyList<string> LiteralPatterns { get; init; } = [];
}

public partial class SecretRedactor
{
    private readonly SecretRedactionOptions _options;

    public SecretRedactor(SecretRedactionOptions options)
    {
        _options = options;
    }

    public string Redact(string text)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var redacted = SecretKeyValueRegex().Replace(
            text,
            match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{_options.Replacement}");
        redacted = ConnectionStringPasswordRegex().Replace(
            redacted,
            match => $"{match.Groups["key"].Value}={_options.Replacement}");
        redacted = PrivateKeyBlockRegex().Replace(redacted, _options.Replacement);

        foreach (var pattern in _options.LiteralPatterns.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            redacted = redacted.Replace(pattern, _options.Replacement, StringComparison.OrdinalIgnoreCase);
        }

        return redacted;
    }

    [GeneratedRegex(@"(?<key>(?i:password|pwd|secret|api[_-]?key|token))(?<separator>\s*[:=]\s*)(?<value>[^;\s]+)")]
    private static partial Regex SecretKeyValueRegex();

    [GeneratedRegex(@"(?<key>(?i:password|pwd|user\s*id|userid))=(?<value>[^;]+)")]
    private static partial Regex ConnectionStringPasswordRegex();

    [GeneratedRegex(@"-----BEGIN [^-]+ PRIVATE KEY-----.*?-----END [^-]+ PRIVATE KEY-----", RegexOptions.Singleline)]
    private static partial Regex PrivateKeyBlockRegex();
}
