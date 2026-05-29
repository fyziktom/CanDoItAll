using CanDoItAll.AgentFramework.Models;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceFileService : IWorkspaceFileService
{
    private readonly WorkspacePathPolicy pathPolicy;
    private readonly WorkspaceFileQueryService queryService;
    private readonly WorkspaceFileMutationService mutationService;

    public WorkspaceFileService(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null)
    {
        pathPolicy = new WorkspacePathPolicy(workspaceRoot, workspaceScope);
        var receiptWriter = new WorkspaceFileReceiptWriter(pathPolicy.WorkspaceRoot, pathPolicy.WorkspaceScope);
        var textContentGuard = new WorkspaceTextContentGuard();

        queryService = new WorkspaceFileQueryService(pathPolicy, receiptWriter, textContentGuard);
        mutationService = new WorkspaceFileMutationService(pathPolicy, receiptWriter);
    }

    public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
        => queryService.ListFiles(relativePath, searchPattern, maxResults);

    public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
        => queryService.SearchText(query, relativePath, maxResults);

    public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
        => queryService.ReadTextFile(path, maxCharacters);

    public WorkspacePathStatResult StatPath(string path)
        => queryService.StatPath(path);

    public WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760)
    {
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return new WorkspacePathHashResult(
                Succeeded: false,
                Message: validationMessage,
                Path: string.Empty,
                PathKind: "missing",
                Algorithm: "SHA-256",
                Hash: string.Empty,
                SizeBytes: 0,
                FileCount: 0,
                IsTruncated: false);
        }

        var limitFiles = Math.Clamp(maxFiles, 1, 2000);
        var limitBytes = Math.Clamp(maxBytes, 1, 100 * 1024 * 1024);
        if (File.Exists(resolution.FullPath))
        {
            var info = new FileInfo(resolution.FullPath);
            if (info.Length > limitBytes)
            {
                return CreateHashFailure(resolution.RelativePath, "file", $"File '{resolution.RelativePath}' exceeds the configured hash byte limit.");
            }

            using var stream = File.OpenRead(resolution.FullPath);
            var hash = SHA256.HashData(stream);
            return new WorkspacePathHashResult(
                Succeeded: true,
                Message: $"Computed SHA-256 for '{resolution.RelativePath}'.",
                Path: resolution.RelativePath,
                PathKind: "file",
                Algorithm: "SHA-256",
                Hash: Convert.ToHexString(hash).ToLowerInvariant(),
                SizeBytes: info.Length,
                FileCount: 1,
                IsTruncated: false);
        }

        if (!Directory.Exists(resolution.FullPath))
        {
            return CreateHashFailure(resolution.RelativePath, "missing", $"Path '{resolution.RelativePath}' does not exist in the workspace.");
        }

        var files = Directory.EnumerateFiles(
                resolution.FullPath,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0
                })
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length > limitFiles)
        {
            return CreateHashFailure(resolution.RelativePath, "directory", $"Directory '{resolution.RelativePath}' exceeds the configured hash file limit.");
        }

        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long totalBytes = 0;
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            totalBytes += info.Length;
            if (totalBytes > limitBytes)
            {
                return CreateHashFailure(resolution.RelativePath, "directory", $"Directory '{resolution.RelativePath}' exceeds the configured hash byte limit.");
            }

            var relativePath = pathPolicy.ToRelativePath(file);
            incrementalHash.AppendData(Encoding.UTF8.GetBytes(relativePath));
            incrementalHash.AppendData([0]);
            using var stream = File.OpenRead(file);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                incrementalHash.AppendData(buffer, 0, read);
            }
        }

        return new WorkspacePathHashResult(
            Succeeded: true,
            Message: $"Computed SHA-256 manifest hash for directory '{resolution.RelativePath}'.",
            Path: resolution.RelativePath,
            PathKind: "directory",
            Algorithm: "SHA-256",
            Hash: Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant(),
            SizeBytes: totalBytes,
            FileCount: files.Length,
            IsTruncated: false);
    }

    public WorkspaceFileMutationResult CreateDirectory(string path)
        => mutationService.CreateDirectory(path);

    public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true)
        => mutationService.WriteTextFile(path, content, overwrite);

    public WorkspaceFileMutationResult AppendTextFile(string path, string content)
        => mutationService.AppendTextFile(path, content);

    public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
        => mutationService.CopyPath(sourcePath, destinationPath, overwrite);

    public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
        => mutationService.MovePath(sourcePath, destinationPath, overwrite);

    public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
        => mutationService.DeletePath(path, recursive);

    public WorkspaceArchiveMutationResult ZipPath(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760)
    {
        if (!pathPolicy.TryResolveWorkspacePath(sourcePath, allowWorkspaceRoot: false, out var source, out var sourceValidation))
        {
            return CreateArchiveFailure(sourceValidation, string.Empty, string.Empty);
        }

        if (!pathPolicy.TryResolveWorkspacePath(destinationPath, allowWorkspaceRoot: false, out var destination, out var destinationValidation))
        {
            return CreateArchiveFailure(destinationValidation, source.RelativePath, string.Empty);
        }

        if (Directory.Exists(destination.FullPath))
        {
            return CreateArchiveFailure($"Destination '{destination.RelativePath}' is a directory.", source.RelativePath, destination.RelativePath);
        }

        if (File.Exists(destination.FullPath) && !overwrite)
        {
            return CreateArchiveFailure($"Destination archive '{destination.RelativePath}' already exists. Set overwrite to true to replace it.", source.RelativePath, destination.RelativePath);
        }

        if (File.Exists(destination.FullPath) && overwrite)
        {
            File.Delete(destination.FullPath);
        }

        var files = ResolveArchiveSourceFiles(source, out var sourceKind);
        if (files.Count == 0)
        {
            return CreateArchiveFailure($"Source path '{source.RelativePath}' does not exist or has no files to archive.", source.RelativePath, destination.RelativePath);
        }

        if (!CheckArchiveBounds(files, maxFiles, maxBytes, out var totalBytes, out var boundMessage))
        {
            return CreateArchiveFailure(boundMessage, source.RelativePath, destination.RelativePath);
        }

        var directory = Path.GetDirectoryName(destination.FullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var archive = ZipFile.Open(destination.FullPath, ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                var entryName = sourceKind == "file"
                    ? Path.GetFileName(file)
                    : Path.GetRelativePath(source.FullPath, file).Replace('\\', '/');
                archive.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
            }
        }

        return new WorkspaceArchiveMutationResult(
            Succeeded: true,
            Message: $"Created archive '{destination.RelativePath}' from '{source.RelativePath}'.",
            SourcePath: source.RelativePath,
            DestinationPath: destination.RelativePath,
            FileCount: files.Count,
            TotalBytes: totalBytes,
            IsTruncated: false);
    }

    public WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760)
    {
        if (!pathPolicy.TryResolveWorkspacePath(sourcePath, allowWorkspaceRoot: false, out var source, out var sourceValidation))
        {
            return CreateArchiveFailure(sourceValidation, string.Empty, string.Empty);
        }

        if (!File.Exists(source.FullPath))
        {
            return CreateArchiveFailure($"Source archive '{source.RelativePath}' does not exist in the workspace.", source.RelativePath, string.Empty);
        }

        if (!pathPolicy.TryResolveWorkspacePath(destinationPath, allowWorkspaceRoot: false, out var destination, out var destinationValidation))
        {
            return CreateArchiveFailure(destinationValidation, source.RelativePath, string.Empty);
        }

        if (File.Exists(destination.FullPath))
        {
            return CreateArchiveFailure($"Destination '{destination.RelativePath}' is a file.", source.RelativePath, destination.RelativePath);
        }

        using var archive = ZipFile.OpenRead(source.FullPath);
        var entries = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToArray();
        if (entries.Length > Math.Clamp(maxFiles, 1, 2000))
        {
            return CreateArchiveFailure($"Archive '{source.RelativePath}' exceeds the configured file limit.", source.RelativePath, destination.RelativePath);
        }

        var totalBytes = entries.Sum(entry => entry.Length);
        if (totalBytes > Math.Clamp(maxBytes, 1, 100 * 1024 * 1024))
        {
            return CreateArchiveFailure($"Archive '{source.RelativePath}' exceeds the configured byte limit.", source.RelativePath, destination.RelativePath);
        }

        Directory.CreateDirectory(destination.FullPath);
        foreach (var entry in entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(destination.FullPath, entry.FullName));
            if (!WorkspacePathPolicy.IsPathWithinRoot(targetPath, destination.FullPath))
            {
                return CreateArchiveFailure($"Archive entry '{entry.FullName}' escapes the destination directory.", source.RelativePath, destination.RelativePath);
            }

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetPath) && !overwrite)
            {
                return CreateArchiveFailure($"Archive target '{pathPolicy.ToRelativePath(targetPath)}' already exists. Set overwrite to true to replace it.", source.RelativePath, destination.RelativePath);
            }

            entry.ExtractToFile(targetPath, overwrite);
        }

        return new WorkspaceArchiveMutationResult(
            Succeeded: true,
            Message: $"Extracted archive '{source.RelativePath}' to '{destination.RelativePath}'.",
            SourcePath: source.RelativePath,
            DestinationPath: destination.RelativePath,
            FileCount: entries.Length,
            TotalBytes: totalBytes,
            IsTruncated: false);
    }

    public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
        => queryService.DiffTextFiles(leftPath, rightPath, maxLines);

    private static WorkspacePathHashResult CreateHashFailure(string path, string pathKind, string message)
        => new(
            Succeeded: false,
            Message: message,
            Path: path,
            PathKind: pathKind,
            Algorithm: "SHA-256",
            Hash: string.Empty,
            SizeBytes: 0,
            FileCount: 0,
            IsTruncated: false);

    private static WorkspaceArchiveMutationResult CreateArchiveFailure(string message, string sourcePath, string destinationPath)
        => new(
            Succeeded: false,
            Message: message,
            SourcePath: sourcePath,
            DestinationPath: destinationPath,
            FileCount: 0,
            TotalBytes: 0,
            IsTruncated: false);

    private static IReadOnlyList<string> ResolveArchiveSourceFiles(
        WorkspacePathResolution source,
        out string sourceKind)
    {
        if (File.Exists(source.FullPath))
        {
            sourceKind = "file";
            return [source.FullPath];
        }

        if (!Directory.Exists(source.FullPath))
        {
            sourceKind = "missing";
            return [];
        }

        sourceKind = "directory";
        return Directory.EnumerateFiles(
                source.FullPath,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0
                })
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool CheckArchiveBounds(
        IReadOnlyList<string> files,
        int maxFiles,
        long maxBytes,
        out long totalBytes,
        out string message)
    {
        totalBytes = 0;
        message = string.Empty;
        if (files.Count > Math.Clamp(maxFiles, 1, 2000))
        {
            message = "Archive operation exceeds the configured file limit.";
            return false;
        }

        var byteLimit = Math.Clamp(maxBytes, 1, 100 * 1024 * 1024);
        foreach (var file in files)
        {
            totalBytes += new FileInfo(file).Length;
            if (totalBytes > byteLimit)
            {
                message = "Archive operation exceeds the configured byte limit.";
                return false;
            }
        }

        return true;
    }
}
