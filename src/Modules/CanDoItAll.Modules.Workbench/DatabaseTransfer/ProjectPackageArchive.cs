using System.IO.Compression;
using System.Security.Cryptography;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectPackageArchive
{
    internal const int CopyBufferSize = 81920;
    internal const int MaximumArchiveEntries = 4096;
    internal const long MaximumArchiveEntryBytes = 256L * 1024L * 1024L;
    internal const long MaximumArchiveBytes = 1024L * 1024L * 1024L;

    internal static async Task CreatePackageArchiveAsync(
        string workingRoot,
        string packagePath,
        DateTimeOffset createdUtc,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        CancellationToken cancellationToken)
    {
        var workingPathPolicy = physicalPathPolicyFactory.Create(workingRoot);
        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(packagePath))
            ?? throw new InvalidDataException("The project package destination does not have a directory parent.");
        var packagePathPolicy = physicalPathPolicyFactory.Create(packageDirectory);
        var stagingPath = $"{packagePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            EnsureTrustedPath(packagePathPolicy, stagingPath, allowMissingLeaf: true);
            await using (var fileStream = new FileStream(
                             stagingPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             CopyBufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var archive = new ZipArchive(
                       fileStream,
                       ZipArchiveMode.Create,
                       leaveOpen: false))
            {
                var sourcePaths = EnumerateTrustedFiles(workingPathPolicy);
                if (sourcePaths.Count > MaximumArchiveEntries)
                {
                    throw new InvalidDataException(
                        "The project package contains too many archive entries.");
                }

                long declaredTotal = 0;
                foreach (var sourcePath in sourcePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sourceLength = new FileInfo(sourcePath).Length;
                    if (sourceLength < 0 || sourceLength > MaximumArchiveEntryBytes)
                    {
                        throw new InvalidDataException(
                            "A project package export payload exceeds the supported entry size.");
                    }

                    declaredTotal = checked(declaredTotal + sourceLength);
                    if (declaredTotal > MaximumArchiveBytes)
                    {
                        throw new InvalidDataException(
                            "The project package export exceeds the supported expanded size.");
                    }

                    var relativePath = Path.GetRelativePath(workingRoot, sourcePath)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    relativePath = NormalizePackageRelativePath(
                        relativePath,
                        isDirectory: false);
                    var entry = archive.CreateEntry(
                        relativePath,
                        CompressionLevel.Optimal);
                    entry.LastWriteTime = createdUtc;
                    await using var entryStream = entry.Open();
                    await using var sourceStream = new FileStream(
                        sourcePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        CopyBufferSize,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    var copiedLength = await CopyBoundedAsync(
                        sourceStream,
                        entryStream,
                        MaximumArchiveEntryBytes,
                        cancellationToken);
                    if (copiedLength != sourceLength)
                    {
                        throw new InvalidDataException(
                            "A project package export payload changed while it was being archived.");
                    }
                }
            }

            packagePathPolicy.RevalidateMutationTarget(packagePath);
            File.Move(stagingPath, packagePath, overwrite: false);
            EnsureTrustedPath(packagePathPolicy, packagePath);
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    internal static async Task<string> ExtractPackageAsync(
        string packagePath,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException(
                "The selected project package was not found.",
                packagePath);
        }

        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(packagePath))
            ?? throw new InvalidDataException("The project package path does not have a directory parent.");
        var packagePathPolicy = physicalPathPolicyFactory.Create(packageDirectory);
        EnsureTrustedPath(packagePathPolicy, packagePath);

        var packageLength = new FileInfo(packagePath).Length;
        if (packageLength > MaximumArchiveBytes)
        {
            throw new InvalidDataException(
                "The project package archive exceeds the supported compressed size.");
        }

        var extractionRoot = Path.Combine(
            Path.GetDirectoryName(packagePath)!,
            $"{Path.GetFileNameWithoutExtension(packagePath)}.{Guid.NewGuid():N}.extract");
        Directory.CreateDirectory(extractionRoot);
        var extractionPathPolicy = physicalPathPolicyFactory.Create(extractionRoot);

        try
        {
            await using var packageStream = new FileStream(
                packagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (packageStream.Length > MaximumArchiveBytes)
            {
                throw new InvalidDataException(
                    "The project package archive exceeds the supported compressed size.");
            }

            using var archive = new ZipArchive(
                packageStream,
                ZipArchiveMode.Read,
                leaveOpen: false);
            if (archive.Entries.Count > MaximumArchiveEntries)
            {
                throw new InvalidDataException(
                    "The project package contains too many archive entries.");
            }

            var entryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long declaredTotal = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isDirectory = string.IsNullOrEmpty(entry.Name);
                var relativePath = NormalizePackageRelativePath(
                    entry.FullName,
                    isDirectory);
                if (!entryPaths.Add(relativePath))
                {
                    throw new InvalidDataException(
                        "The project package contains duplicate archive paths.");
                }

                if (IsSymbolicLinkEntry(entry))
                {
                    throw new InvalidDataException(
                        "The project package contains a symbolic-link entry.");
                }

                if (entry.Length < 0 || entry.Length > MaximumArchiveEntryBytes)
                {
                    throw new InvalidDataException(
                        $"Project package entry '{relativePath}' exceeds the supported size.");
                }

                declaredTotal = checked(declaredTotal + entry.Length);
                if (declaredTotal > MaximumArchiveBytes)
                {
                    throw new InvalidDataException(
                        "The project package expands beyond the supported total size.");
                }

                var destinationPath = ResolvePackageFilePath(
                    extractionPathPolicy,
                    relativePath);
                if (isDirectory)
                {
                    if (entry.Length != 0)
                    {
                        throw new InvalidDataException(
                            "The project package contains a non-empty directory entry.");
                    }

                    Directory.CreateDirectory(destinationPath);
                    EnsureTrustedPath(extractionPathPolicy, destinationPath);
                    continue;
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath)!;
                Directory.CreateDirectory(destinationDirectory);
                EnsureTrustedPath(extractionPathPolicy, destinationDirectory);
                extractionPathPolicy.RevalidateMutationTarget(destinationPath);
                await using var sourceStream = entry.Open();
                var integrity = await CopyToNewFileWithIntegrityAsync(
                    sourceStream,
                    destinationPath,
                    MaximumArchiveEntryBytes,
                    cancellationToken);
                if (integrity.Length != entry.Length)
                {
                    throw new InvalidDataException(
                        $"Project package entry '{relativePath}' length changed while extracting.");
                }

                EnsureTrustedPath(extractionPathPolicy, destinationPath);
            }

            return extractionRoot;
        }
        catch
        {
            DeleteDirectoryIfExists(extractionRoot);
            throw;
        }
    }

    internal static string ResolvePackageFilePath(
        string root,
        string relativePath,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
        => ResolvePackageFilePath(physicalPathPolicyFactory.Create(root), relativePath);

    private static string ResolvePackageFilePath(
        IPhysicalFileSystemPathPolicy rootPathPolicy,
        string relativePath)
    {
        var normalizedRelativePath = NormalizePackageRelativePath(
            relativePath,
            isDirectory: false);
        try
        {
            return rootPathPolicy.ResolveContainedPath(
                normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }
        catch (PhysicalPathValidationException exception)
        {
            throw new InvalidDataException(
                "The project package contains an unsafe path outside its trusted root.",
                exception);
        }
    }

    internal static string NormalizePackageRelativePath(
        string value,
        bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 1024 ||
            value.Contains('\\') ||
            Path.IsPathRooted(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The project package contains a noncanonical relative path.");
        }

        var normalized = isDirectory && value.EndsWith("/", StringComparison.Ordinal)
            ? value[..^1]
            : value;
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Length == 0 ||
            segments.Any(segment =>
                string.IsNullOrEmpty(segment) ||
                segment is "." or ".." ||
                segment.Length > 255 ||
                segment.Contains(':') ||
                segment.Any(char.IsControl) ||
                segment.EndsWith(' ') ||
                segment.EndsWith('.')))
        {
            throw new InvalidDataException(
                "The project package contains a noncanonical relative path.");
        }

        return string.Join('/', segments);
    }

    internal static async Task<FileIntegrity> CopyToNewFileWithIntegrityAsync(
        Stream source,
        string destinationPath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[CopyBufferSize];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > maximumBytes)
            {
                throw new InvalidDataException(
                    "A project package payload exceeds the supported size.");
            }

            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        await destination.FlushAsync(cancellationToken);
        return new FileIntegrity(
            total,
            Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    internal static async Task<FileIntegrity> ComputeFileIntegrityAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ComputeStreamIntegrityAsync(
            stream,
            maximumBytes,
            cancellationToken);
    }

    internal static async Task<FileIntegrity> ComputeStreamIntegrityAsync(
        Stream stream,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[CopyBufferSize];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > maximumBytes)
            {
                throw new InvalidDataException(
                    "A project package payload exceeds the supported size.");
            }

            hash.AppendData(buffer, 0, read);
        }

        return new FileIntegrity(
            total,
            Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    internal static async Task VerifyFileIntegrityAsync(
        string path,
        long expectedLength,
        string expectedSha256,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (expectedLength < 0 ||
            expectedLength > maximumBytes ||
            !IsSha256(expectedSha256))
        {
            throw new InvalidDataException(
                "The project package contains invalid payload integrity metadata.");
        }

        var integrity = await ComputeFileIntegrityAsync(
            path,
            maximumBytes,
            cancellationToken);
        if (integrity.Length != expectedLength ||
            !string.Equals(
                integrity.Sha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A project package payload failed its length or SHA-256 integrity check.");
        }
    }

    internal static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var length = new FileInfo(path).Length;
        if (length < 0 || length > maximumBytes || length > int.MaxValue)
        {
            throw new InvalidDataException(
                "A project package storage payload exceeds the supported size.");
        }

        var content = new byte[(int)length];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.ReadExactlyAsync(content, cancellationToken);
        return content;
    }

    internal static bool IsSha256(string value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static IReadOnlyList<string> EnumerateTrustedFiles(
        IPhysicalFileSystemPathPolicy rootPathPolicy)
    {
        var rootPath = rootPathPolicy.RootPath;
        EnsureTrustedPath(rootPathPolicy, rootPath);
        var files = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);
        while (pendingDirectories.TryPop(out var directory))
        {
            EnsureTrustedPath(rootPathPolicy, directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(
                         directory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                EnsureTrustedPath(rootPathPolicy, entry);
                if (Directory.Exists(entry))
                {
                    pendingDirectories.Push(entry);
                    continue;
                }

                if (!File.Exists(entry))
                {
                    throw new InvalidDataException(
                        "The project package staging tree contains an unsupported filesystem object.");
                }

                files.Add(entry);
            }
        }

        return files
            .OrderBy(
                path => NormalizeEnumerationKey(Path.GetRelativePath(rootPath, path)),
                StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<long> CopyBoundedAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return total;
            }

            total = checked(total + read);
            if (total > maximumBytes)
            {
                throw new InvalidDataException(
                    "A project package payload exceeds the supported size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    internal static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static bool IsSymbolicLinkEntry(ZipArchiveEntry entry)
    {
        const int UnixFileTypeMask = 0xF000;
        const int UnixSymbolicLink = 0xA000;
        var unixMode = entry.ExternalAttributes >> 16;
        return (unixMode & UnixFileTypeMask) == UnixSymbolicLink;
    }

    private static void EnsureTrustedPath(
        IPhysicalFileSystemPathPolicy rootPathPolicy,
        string path,
        bool allowMissingLeaf = false)
    {
        try
        {
            rootPathPolicy.EnsureSafePath(path, allowMissingLeaf);
        }
        catch (PhysicalPathValidationException exception)
        {
            throw new InvalidDataException(
                "Project package paths cannot escape or traverse links in their trusted root.",
                exception);
        }
    }

    private static string NormalizeEnumerationKey(string path)
        => path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
}

internal sealed record FileIntegrity(long Length, string Sha256);
