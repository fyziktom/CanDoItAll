using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CanDoItAll.Modules.Workbench;

internal static class WindowsFileSystemObjectIdentity
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExistingDisposition = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint VolumeNameNt = 0x00000002;
    private const string ExtendedPathPrefix = @"\\?\";
    private const string ExtendedUncPathPrefix = @"\\?\UNC\";
    private const string DevicePathPrefix = @"\\.\";
    private const string UncPathPrefix = @"\\";

    internal static string ResolveCanonicalPath(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        var normalizedPath = Path.GetFullPath(fullPath);
        var existingPath = normalizedPath;
        var missingSegments = new Stack<string>();
        while (!File.Exists(existingPath) && !Directory.Exists(existingPath))
        {
            var segment = Path.GetFileName(existingPath);
            if (string.IsNullOrEmpty(segment))
            {
                throw new InvalidOperationException(
                    "No existing filesystem ancestor can prove the managed object identity.");
            }

            missingSegments.Push(segment);
            existingPath = Path.GetDirectoryName(existingPath)
                ?? throw new InvalidOperationException(
                    "No existing filesystem ancestor can prove the managed object identity.");
        }

        var canonicalPath = ResolveExistingCanonicalPath(existingPath);
        while (missingSegments.TryPop(out var segment))
        {
            canonicalPath = Path.Combine(canonicalPath, segment);
        }

        return canonicalPath;
    }

    internal static string ResolveConservativePhysicalIdentity(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        var normalizedPath = Path.GetFullPath(fullPath);
        if (!File.Exists(normalizedPath) && !Directory.Exists(normalizedPath))
        {
            return $"absent|{ResolveCanonicalPath(normalizedPath)}";
        }

        using var handle = OpenExisting(normalizedPath);
        if (!GetFileInformationByHandle(handle, out var information))
        {
            var error = Marshal.GetLastPInvokeError();
            throw new Win32Exception(
                error,
                $"Windows could not resolve the managed object's physical identity (Win32 error {error}).");
        }

        return $"object|{information.VolumeSerialNumber:x8}|{information.FileIndexHigh:x8}{information.FileIndexLow:x8}";
    }

    private static string ResolveExistingCanonicalPath(string existingPath)
    {
        using var handle = OpenExisting(existingPath);
        var capacity = 512;
        while (true)
        {
            var buffer = new char[capacity];
            var length = GetFinalPathNameByHandle(
                handle,
                buffer,
                (uint)buffer.Length,
                VolumeNameNt);
            if (length == 0)
            {
                var error = Marshal.GetLastPInvokeError();
                throw new Win32Exception(
                    error,
                    $"Windows could not resolve the managed object's final path identity (Win32 error {error}).");
            }

            if (length < buffer.Length)
            {
                return new string(buffer, 0, checked((int)length));
            }

            capacity = checked((int)length + 1);
        }
    }

    private static SafeFileHandle OpenExisting(string path)
    {
        var handle = CreateFile(
            ResolveCreateFilePath(path),
            desiredAccess: 0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExistingDisposition,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(
                error,
                $"Windows could not open the managed object to prove its filesystem identity (Win32 error {error}).");
        }

        return handle;
    }

    internal static string ResolveCreateFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal) ||
            path.StartsWith(DevicePathPrefix, StringComparison.Ordinal))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(UncPathPrefix, StringComparison.Ordinal)
            ? $"{ExtendedUncPathPrefix}{fullPath[UncPathPrefix.Length..]}"
            : $"{ExtendedPathPrefix}{fullPath}";
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        [Out] char[] filePath,
        uint filePathLength,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }
}
