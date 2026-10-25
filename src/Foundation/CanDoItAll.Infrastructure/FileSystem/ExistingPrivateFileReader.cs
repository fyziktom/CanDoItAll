using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure;

internal static class ExistingPrivateFileReader {
    private const UnixFileMode SharedAccess = UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
        UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    internal static string RequireDirectory(string root, string directory) {
        var policy = new PhysicalFileSystemPathPolicyFactory().Create(root, PhysicalFileSystemCaseSensitivity.Sensitive);
        string path = policy.ResolveContainedPath(directory);
        policy.EnsureSafePath(path);
        for (string current = path; ; current = Path.GetDirectoryName(current)!) {
            if ((File.GetAttributes(current) & FileAttributes.Directory) == 0) {
                throw new IOException("Read-only inspection requires existing directories.");
            }
            RequirePrivate(current);
            if (string.Equals(current, policy.RootPath, StringComparison.Ordinal)) {
                return path;
            }
        }
    }

    internal static byte[] Read(string root, string path, int maximumBytes) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        var policy = new PhysicalFileSystemPathPolicyFactory().Create(root, PhysicalFileSystemCaseSensitivity.Sensitive);
        string fullPath = policy.ResolveContainedPath(path);
        RequireDirectory(root, Path.GetDirectoryName(fullPath)!);
        policy.EnsureSafePath(fullPath);
        RequirePrivate(fullPath);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > maximumBytes) {
            throw new InvalidDataException("The existing inspection file exceeds its supported bounds.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) {
            throw new InvalidDataException("The existing inspection file changed during the read.");
        }
        policy.EnsureSafePath(fullPath);
        RequirePrivate(fullPath);
        return bytes;
    }

    private static void RequirePrivate(string path) {
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & SharedAccess) != 0) {
            throw new InvalidOperationException("Read-only inspection requires private existing file permissions.");
        }
    }
}
