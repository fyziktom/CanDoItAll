using System.Text.Json;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal sealed class ApiAuthorityFileRead(IPhysicalFileSystemPathPolicy policy, string directory) {
    internal const int MaximumAccountBytes = 64 * 1024 * 1024;
    internal const int MaximumTokenBytes = 64 * 1024;
    internal const int MaximumHeaderBytes = 4 * 1024;
    private const UnixFileMode PrivateDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    internal string DirectoryPath { get; } = directory;

    internal static ApiAuthorityFileRead Prepare(IControlPlanePathResolver paths, DurableFileWriter writer,
        IPhysicalFileSystemPathPolicyFactory factory, string directoryName) {
        var root = paths.ResolveRootPath();
        var directory = Path.Combine(root, directoryName);
        writer.EnsureDirectory(root, directory, requirePrivateUnixMode: true);
        var location = new ApiAuthorityFileRead(factory.Create(root), directory);
        location.ValidateDirectory();
        return location;
    }

    internal void ValidateDirectory() {
        ValidatePath(policy.RootPath, isDirectory: true);
        ValidatePath(DirectoryPath, isDirectory: true);
    }

    internal async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string fileName, int maximumBytes,
        CancellationToken cancellationToken, bool prefixOnly = false) {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateDirectory();
        var path = Path.Combine(DirectoryPath, fileName);
        ValidatePath(path, isDirectory: false);
        await using var stream = OpenRead(path);
        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(stream.SafeFileHandle) != PrivateFileMode) {
            throw new ApiAuthorityReadException(ApiAuthorityReadFailure.UnsafePath);
        }
        var bytes = await ReadBoundedAsync(stream, maximumBytes, cancellationToken, prefixOnly).ConfigureAwait(false);
        ValidateDirectory();
        return bytes;
    }

    internal static async ValueTask<ReadOnlyMemory<byte>> ReadFileAsync(string path, int maximumBytes,
        CancellationToken cancellationToken) {
        await using var stream = OpenRead(path);
        return await ReadBoundedAsync(stream, maximumBytes, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<ReadOnlyMemory<byte>> ReadBoundedAsync(Stream stream, int maximumBytes,
        CancellationToken cancellationToken, bool prefixOnly = false) {
        cancellationToken.ThrowIfCancellationRequested();
        var length = stream.Length;
        if (!prefixOnly && length > maximumBytes) {
            throw new ApiAuthorityReadException(ApiAuthorityReadFailure.SizeLimit);
        }
        var capacity = prefixOnly ? maximumBytes : checked((int)Math.Min(length + 1, maximumBytes + 1L));
        var bytes = new byte[capacity];
        var count = 0;
        while (true) {
            var read = await stream.ReadAsync(bytes.AsMemory(count), cancellationToken).ConfigureAwait(false);
            count += read;
            if (!prefixOnly && count > maximumBytes) {
                throw new ApiAuthorityReadException(ApiAuthorityReadFailure.SizeLimit);
            }
            if (read == 0 || prefixOnly && count == maximumBytes) {
                return bytes.AsMemory(0, count);
            }
            if (count == bytes.Length) {
                Array.Resize(ref bytes, (int)Math.Min((long)bytes.Length * 2, maximumBytes + 1L));
            }
        }
    }

    internal static ApiAuthorityReadFailure? Classify(Exception exception) => exception switch {
        ApiAuthorityReadException failure => failure.Failure,
        PhysicalPathValidationException => ApiAuthorityReadFailure.UnsafePath,
        FileNotFoundException or DirectoryNotFoundException => ApiAuthorityReadFailure.MissingPath,
        UnauthorizedAccessException => ApiAuthorityReadFailure.UnsafePath,
        InvalidDataException or JsonException => ApiAuthorityReadFailure.InvalidFormat,
        IOException => ApiAuthorityReadFailure.IoFailure,
        _ => null
    };

    private void ValidatePath(string path, bool isDirectory) {
        policy.EnsureSafePath(path, allowMissingLeaf: true);
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.Directory) != isDirectory || attributes.HasFlag(FileAttributes.ReparsePoint)) {
            throw new ApiAuthorityReadException(ApiAuthorityReadFailure.UnsafePath);
        }
        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(path) != (isDirectory ? PrivateDirectoryMode : PrivateFileMode)) {
            throw new ApiAuthorityReadException(ApiAuthorityReadFailure.UnsafePath);
        }
    }

    internal static FileStream OpenRead(string path) => new(path, new FileStreamOptions {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read | FileShare.Delete,
        Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        BufferSize = 4096
    });
}
