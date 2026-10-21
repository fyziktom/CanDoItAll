using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class WorkspaceSeedInputReader {
    private readonly Func<WorkspaceSeedRoots> resolveRoots;
    private readonly Func<string, Stream> openRead;
    private readonly int maximumFiles;
    private readonly long maximumBytes;
    private readonly Dictionary<string, (long Length, string Digest)> files = new(StringComparer.Ordinal);
    private long retainedBytes;
    private bool canRetain = true;

    internal WorkspaceSeedInputReader(Func<WorkspaceSeedRoots>? resolveRoots = null,
        Func<string, Stream>? openRead = null, int maximumFiles = 512, long maximumBytes = 8 * 1024 * 1024) {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumFiles);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        this.resolveRoots = resolveRoots ?? (() => new(CapabilityTemplatePackLoader.FindPackRoot(), AgentTemplatePackLoader.FindPackRoot()));
        this.openRead = openRead ?? File.OpenRead;
        this.maximumFiles = maximumFiles;
        this.maximumBytes = maximumBytes;
        Roots = this.resolveRoots();
    }

    internal WorkspaceSeedRoots Roots { get; }

    internal string ReadText(string path) {
        path = Path.GetFullPath(path);
        using var stream = openRead(path);
        var length = stream.Length;
        if (!canRetain || length > maximumBytes - retainedBytes || files.Count >= maximumFiles) {
            canRetain = false;
            files.Clear();
            using var uncached = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return uncached.ReadToEnd();
        }
        var bytes = new byte[checked((int)length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) {
            throw new IOException("A seed template changed size while it was being read.");
        }
        var identity = (length, Convert.ToHexString(SHA256.HashData(bytes)));
        if (files.TryGetValue(path, out var previous)) {
            canRetain &= previous == identity;
        } else {
            files.Add(path, identity);
            retainedBytes += length;
        }
        using var text = new StreamReader(new MemoryStream(bytes, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return text.ReadToEnd();
    }

    internal bool IsCurrent() {
        if (!canRetain || files.Count == 0 || Roots != resolveRoots()) {
            return false;
        }
        try {
            foreach (var (path, expected) in files) {
                using var stream = openRead(path);
                if (stream.Length != expected.Length || Convert.ToHexString(SHA256.HashData(stream)) != expected.Digest) {
                    return false;
                }
            }
            return true;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return false;
        }
    }
}

internal sealed record WorkspaceSeedRoots(string Capabilities, string Agents);
