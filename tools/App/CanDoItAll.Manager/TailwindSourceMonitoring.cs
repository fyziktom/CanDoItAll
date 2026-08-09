using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Manager;

internal enum TailwindWatchRootKind
{
    TailwindWorkspace,
    ContentSource
}

[Flags]
internal enum TailwindWatchSignalKind
{
    None = 0,
    FileSystemEvent = 1,
    WatcherError = 2,
    Poll = 4
}

internal sealed record TailwindWatchRoot(
    int Id,
    string FullPath,
    TailwindWatchRootKind Kind,
    IPhysicalFileSystemPathPolicy PathPolicy)
{
    public string CreateSignalKey(string fullPath)
    {
        string normalizedPath = Path.GetFullPath(fullPath);
        string relativePath = Path.GetRelativePath(PathPolicy.RootPath, normalizedPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        string canonicalPath = PathPolicy.CaseSensitivity == PhysicalFileSystemCaseSensitivity.Insensitive
            ? relativePath.ToUpperInvariant()
            : relativePath;
        return $"{Id}:{canonicalPath}";
    }
}

internal sealed record TailwindWatchSignalBatch(
    long Generation,
    TailwindWatchSignalKind Kinds,
    IReadOnlyList<string> ChangedPaths);

internal sealed class TailwindWatchSignalQueue
{
    private readonly Channel<long> signals = Channel.CreateBounded<long>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly object gate = new();
    private readonly Dictionary<string, string> changedPaths = new(StringComparer.Ordinal);
    private long generation;
    private TailwindWatchSignalKind signalKinds;

    public long Signal(TailwindWatchRoot root, string fullPath, TailwindWatchSignalKind kind)
    {
        long nextGeneration;
        lock (gate)
        {
            changedPaths[root.CreateSignalKey(fullPath)] = Path.GetFullPath(fullPath);
            signalKinds |= kind;
            nextGeneration = ++generation;
        }

        signals.Writer.TryWrite(nextGeneration);
        return nextGeneration;
    }

    public async Task<TailwindWatchSignalBatch> ReadBatchAsync(
        TimeSpan debounceWindow,
        CancellationToken cancellationToken)
    {
        long observedGeneration = await signals.Reader.ReadAsync(cancellationToken);
        while (true)
        {
            await Task.Delay(debounceWindow, cancellationToken);
            while (signals.Reader.TryRead(out long nextGeneration))
            {
                observedGeneration = Math.Max(observedGeneration, nextGeneration);
            }

            lock (gate)
            {
                if (observedGeneration < generation)
                {
                    observedGeneration = generation;
                    continue;
                }

                string[] paths = changedPaths
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => entry.Value)
                    .ToArray();
                changedPaths.Clear();
                TailwindWatchSignalKind kinds = signalKinds;
                signalKinds = TailwindWatchSignalKind.None;
                return new TailwindWatchSignalBatch(observedGeneration, kinds, paths);
            }
        }
    }

    public void Complete()
        => signals.Writer.TryComplete();
}

internal static class TailwindSourcePathPolicy
{
    private static readonly HashSet<string> IgnoredPathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".artifacts",
        ".git",
        "bin",
        "node_modules",
        "obj"
    };

    private static readonly HashSet<string> TailwindWorkspaceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css"
    };

    private static readonly HashSet<string> TailwindWorkspaceFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json",
        "package.json"
    };

    private static readonly HashSet<string> TailwindContentSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".cshtml",
        ".html",
        ".js",
        ".jsx",
        ".razor",
        ".ts",
        ".tsx"
    };

    public static bool IsRelevant(TailwindWatchRoot root, string fullPath, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        string normalizedPath = Path.GetFullPath(fullPath);
        if (!root.PathPolicy.IsWithinRoot(normalizedPath) ||
            root.PathPolicy.PathComparer.Equals(normalizedPath, Path.GetFullPath(outputPath)))
        {
            return false;
        }

        string relativePath = Path.GetRelativePath(root.PathPolicy.RootPath, normalizedPath);
        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(IgnoredPathSegments.Contains))
        {
            return false;
        }

        string fileName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string extension = Path.GetExtension(normalizedPath);
        return root.Kind switch
        {
            TailwindWatchRootKind.TailwindWorkspace => TailwindWorkspaceFileNames.Contains(fileName) ||
                                                       TailwindWorkspaceExtensions.Contains(extension),
            TailwindWatchRootKind.ContentSource => TailwindContentSourceExtensions.Contains(extension),
            _ => false
        };
    }
}

internal static class TailwindSourceFingerprint
{
    public static string Compute(IReadOnlyList<TailwindWatchRoot> roots, string outputPath)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (TailwindWatchRoot root in roots.OrderBy(root => root.Id))
        {
            AppendString(hash, root.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!Directory.Exists(root.PathPolicy.RootPath))
            {
                AppendString(hash, "missing");
                continue;
            }

            root.PathPolicy.EnsureSafePath(root.PathPolicy.RootPath);
            var files = Directory.EnumerateFiles(
                    root.PathPolicy.RootPath,
                    "*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = false,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    })
                .Where(path => TailwindSourcePathPolicy.IsRelevant(root, path, outputPath))
                .Select(path => new
                {
                    FullPath = path,
                    LogicalPath = Path.GetRelativePath(root.PathPolicy.RootPath, path)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/')
                })
                .OrderBy(file => file.LogicalPath, StringComparer.Ordinal)
                .ToArray();
            foreach (var file in files)
            {
                root.PathPolicy.EnsureSafePath(file.FullPath);
                AppendString(hash, file.LogicalPath);
                AppendFile(hash, file.FullPath);
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFile(IncrementalHash hash, string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        long length = info.Length;
        DateTime lastWriteTimeUtc = info.LastWriteTimeUtc;
        Span<byte> metadata = stackalloc byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(metadata, length);
        BinaryPrimitives.WriteInt64LittleEndian(metadata[8..], lastWriteTimeUtc.Ticks);
        hash.AppendData(metadata);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        var buffer = new byte[64 * 1024];
        long readLength = 0;
        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            readLength += read;
        }

        info.Refresh();
        if (readLength != length || info.Length != length || info.LastWriteTimeUtc != lastWriteTimeUtc)
        {
            throw new IOException($"Tailwind source '{path}' changed while its fingerprint was captured.");
        }
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
