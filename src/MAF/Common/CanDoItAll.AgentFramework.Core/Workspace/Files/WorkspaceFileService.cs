using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Infrastructure.FileSystem;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceFileService : IWorkspaceFileService
{
    private readonly WorkspacePathPolicy pathPolicy;
    private readonly WorkspaceFileReceiptWriter receiptWriter;
    private readonly WorkspaceFileQueryService queryService;
    private readonly WorkspaceFileMutationService mutationService;
    private readonly WorkspaceDestinationContentPlacementPolicy destinationContentPlacementPolicy;

    public WorkspaceFileService(
        string workspaceRoot,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
    {
        pathPolicy = new WorkspacePathPolicy(
            workspaceRoot,
            physicalPathPolicyFactory,
            workspaceScope,
            externalTargetRegistry);
        receiptWriter = new WorkspaceFileReceiptWriter(pathPolicy.WorkspaceRoot, pathPolicy.WorkspaceScope);
        var textContentGuard = new WorkspaceTextContentGuard();

        queryService = new WorkspaceFileQueryService(pathPolicy, receiptWriter, textContentGuard);
        destinationContentPlacementPolicy = new WorkspaceDestinationContentPlacementPolicy(pathPolicy);
        mutationService = new WorkspaceFileMutationService(pathPolicy, receiptWriter, destinationContentPlacementPolicy);
    }

    public WorkspaceFileListResult ListDirectory(string? relativePath = null, int maxResults = 100)
        => queryService.ListDirectory(relativePath, maxResults);

    public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
        => queryService.ListFiles(relativePath, searchPattern, maxResults);

    public WorkspaceFileListResult ListFiles(
        string path,
        string searchPattern,
        int maxResults,
        string authorityRootPath)
        => queryService.ListFiles(path, searchPattern, maxResults, [authorityRootPath]);

    public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
        => queryService.SearchText(query, relativePath, maxResults);

    public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
        => queryService.ReadTextFile(path, maxCharacters);

    public WorkspaceTextFileReadResult ReadTextFile(
        string path,
        int maxCharacters,
        string authorityRootPath)
        => queryService.ReadTextFile(path, maxCharacters, [authorityRootPath]);

    public WorkspacePathStatResult StatPath(string path)
        => queryService.StatPath(path);

    public WorkspacePathStatResult StatPath(string path, string authorityRootPath)
        => queryService.StatPath(path, [authorityRootPath]);

    public WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        const string operationName = "workspace_hash_path";
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return new WorkspacePathHashResult(
                Succeeded: false,
                Message: validationMessage,
                Receipt: receiptWriter.CreateReceipt(operationName, false, "Denied", validationMessage, string.Empty, [], [], startedAtUtc),
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
            pathPolicy.ValidatePathForUse(resolution.FullPath);
            var info = new FileInfo(resolution.FullPath);
            if (info.Length > limitBytes)
            {
                return CreateHashFailure(operationName, resolution.RelativePath, "file", $"File '{resolution.RelativePath}' exceeds the configured hash byte limit.", startedAtUtc);
            }

            pathPolicy.ValidatePathForUse(resolution.FullPath);
            using var stream = File.OpenRead(resolution.FullPath);
            var hash = SHA256.HashData(stream);
            return new WorkspacePathHashResult(
                Succeeded: true,
                Message: $"Computed SHA-256 for '{resolution.RelativePath}'.",
                Receipt: receiptWriter.CreateReceipt(operationName, false, "Succeeded", $"Computed SHA-256 for '{resolution.RelativePath}'.", string.Empty, [resolution.RelativePath], [], startedAtUtc),
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
            return CreateHashFailure(operationName, resolution.RelativePath, "missing", $"Path '{resolution.RelativePath}' does not exist in the workspace.", startedAtUtc);
        }

        var files = Directory.EnumerateFiles(
                resolution.FullPath,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                })
            .OrderBy(
                file => WorkspacePathPolicy.NormalizeRelativePath(
                    Path.GetRelativePath(resolution.FullPath, file)),
                StringComparer.Ordinal)
            .ThenBy(file => file, StringComparer.Ordinal)
            .ToArray();
        if (files.Length > limitFiles)
        {
            return CreateHashFailure(operationName, resolution.RelativePath, "directory", $"Directory '{resolution.RelativePath}' exceeds the configured hash file limit.", startedAtUtc);
        }

        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long totalBytes = 0;
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            totalBytes += info.Length;
            if (totalBytes > limitBytes)
            {
                return CreateHashFailure(operationName, resolution.RelativePath, "directory", $"Directory '{resolution.RelativePath}' exceeds the configured hash byte limit.", startedAtUtc);
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

        var message = $"Computed SHA-256 manifest hash for directory '{resolution.RelativePath}'.";
        return new WorkspacePathHashResult(
            Succeeded: true,
            Message: message,
            Receipt: receiptWriter.CreateReceipt(operationName, false, "Succeeded", message, string.Empty, [resolution.RelativePath], [], startedAtUtc),
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
        => mutationService.WriteTextFile(path, content, overwrite, authorityRootPath: null);

    public WorkspaceFileMutationResult WriteTextFile(
        string path,
        string content,
        bool overwrite,
        string authorityRootPath)
        => mutationService.WriteTextFile(path, content, overwrite, authorityRootPath);

    public WorkspaceFileMutationResult AppendTextFile(string path, string content)
        => mutationService.AppendTextFile(path, content, authorityRootPath: null);

    public WorkspaceFileMutationResult AppendTextFile(
        string path,
        string content,
        string authorityRootPath)
        => mutationService.AppendTextFile(path, content, authorityRootPath);

    public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
        => mutationService.CopyPath(sourcePath, destinationPath, overwrite, destinationAuthorityRootPath: null);

    public WorkspaceFileMutationResult CopyPath(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        string destinationAuthorityRootPath)
        => mutationService.CopyPath(sourcePath, destinationPath, overwrite, destinationAuthorityRootPath);

    public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
        => mutationService.MovePath(sourcePath, destinationPath, overwrite, destinationAuthorityRootPath: null);

    public WorkspaceFileMutationResult MovePath(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        string destinationAuthorityRootPath)
        => mutationService.MovePath(sourcePath, destinationPath, overwrite, destinationAuthorityRootPath);

    public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
        => mutationService.DeletePath(path, recursive);

    public WorkspaceArchiveMutationResult ZipPath(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        const string operationName = "workspace_zip_path";
        if (!pathPolicy.TryResolveWorkspacePath(sourcePath, allowWorkspaceRoot: false, out var source, out var sourceValidation))
        {
            return CreateArchiveFailure(operationName, sourceValidation, string.Empty, string.Empty, startedAtUtc);
        }

        if (!pathPolicy.TryResolveWorkspacePath(destinationPath, allowWorkspaceRoot: false, out var destination, out var destinationValidation))
        {
            return CreateArchiveFailure(operationName, destinationValidation, source.RelativePath, string.Empty, startedAtUtc);
        }

        if (Directory.Exists(destination.FullPath))
        {
            return CreateArchiveFailure(operationName, $"Destination '{destination.RelativePath}' is a directory.", source.RelativePath, destination.RelativePath, startedAtUtc);
        }

        if (File.Exists(destination.FullPath) && !overwrite)
        {
            return CreateArchiveFailure(operationName, $"Destination archive '{destination.RelativePath}' already exists. Set overwrite to true to replace it.", source.RelativePath, destination.RelativePath, startedAtUtc);
        }

        var files = ResolveArchiveSourceFiles(source, out var sourceKind);
        if (files.Count == 0)
        {
            return CreateArchiveFailure(operationName, $"Source path '{source.RelativePath}' does not exist or has no files to archive.", source.RelativePath, destination.RelativePath, startedAtUtc);
        }

        if (!CheckArchiveBounds(files, maxFiles, maxBytes, out var totalBytes, out var boundMessage))
        {
            return CreateArchiveFailure(operationName, boundMessage, source.RelativePath, destination.RelativePath, startedAtUtc);
        }

        var directory = Path.GetDirectoryName(destination.FullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            pathPolicy.EnsureDirectoryForMutation(directory);
        }

        var stagingArchivePath = WorkspaceFileMutationService.CreateSiblingArtifactPath(
            destination.FullPath,
            "stage");
        pathPolicy.ValidateMutationTarget(stagingArchivePath);
        try
        {
            using (var archive = ZipFile.Open(stagingArchivePath, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    pathPolicy.ValidatePathForUse(file);
                    var entryName = sourceKind == "file"
                        ? Path.GetFileName(file)
                        : Path.GetRelativePath(source.FullPath, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
                }
            }

            pathPolicy.ValidateMutationTarget(stagingArchivePath);
            pathPolicy.ValidateMutationTarget(destination.FullPath);
            File.Move(stagingArchivePath, destination.FullPath, overwrite);
        }
        finally
        {
            if (File.Exists(stagingArchivePath))
            {
                pathPolicy.ValidateMutationTarget(stagingArchivePath);
                File.Delete(stagingArchivePath);
            }
        }

        var message = $"Created archive '{destination.RelativePath}' from '{source.RelativePath}'.";
        return new WorkspaceArchiveMutationResult(
            Succeeded: true,
            Message: message,
            Receipt: receiptWriter.CreateReceipt(operationName, true, "Succeeded", message, string.Empty, [source.RelativePath, destination.RelativePath], [], startedAtUtc),
            SourcePath: source.RelativePath,
            DestinationPath: destination.RelativePath,
            FileCount: files.Count,
            TotalBytes: totalBytes,
            IsTruncated: false);
    }

    public WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760)
        => UnzipArchiveCore(
            sourcePath,
            destinationPath,
            overwrite,
            maxFiles,
            maxBytes,
            destinationAuthorityRootPath: null);

    public WorkspaceArchiveMutationResult UnzipArchive(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        int maxFiles,
        long maxBytes,
        string destinationAuthorityRootPath)
        => UnzipArchiveCore(
            sourcePath,
            destinationPath,
            overwrite,
            maxFiles,
            maxBytes,
            destinationAuthorityRootPath);

    private WorkspaceArchiveMutationResult UnzipArchiveCore(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        int maxFiles,
        long maxBytes,
        string? destinationAuthorityRootPath)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        const string operationName = "workspace_unzip_archive";
        if (!pathPolicy.TryResolveWorkspacePath(sourcePath, allowWorkspaceRoot: false, out var source, out var sourceValidation))
        {
            return CreateArchiveFailure(operationName, sourceValidation, string.Empty, string.Empty, startedAtUtc);
        }

        if (!File.Exists(source.FullPath))
        {
            return CreateArchiveFailure(operationName, $"Source archive '{source.RelativePath}' does not exist in the workspace.", source.RelativePath, string.Empty, startedAtUtc);
        }

        if (!pathPolicy.TryResolveWorkspacePath(destinationPath, allowWorkspaceRoot: false, out var destination, out var destinationValidation))
        {
            return CreateArchiveFailure(operationName, destinationValidation, source.RelativePath, string.Empty, startedAtUtc);
        }

        if (File.Exists(destination.FullPath))
        {
            return CreateArchiveFailure(operationName, $"Destination '{destination.RelativePath}' is a file.", source.RelativePath, destination.RelativePath, startedAtUtc);
        }

        pathPolicy.ValidatePathForUse(source.FullPath);
        using var archive = ZipFile.OpenRead(source.FullPath);
        var entries = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .OrderBy(entry => NormalizeArchiveEntryPath(entry.FullName), StringComparer.Ordinal)
            .ToArray();
        if (entries.Length > Math.Clamp(maxFiles, 1, 2000))
        {
            return CreateArchiveFailure(operationName, $"Archive '{source.RelativePath}' exceeds the configured file limit.", source.RelativePath, destination.RelativePath, startedAtUtc);
        }

        var totalBytes = entries.Sum(entry => entry.Length);
        if (totalBytes > Math.Clamp(maxBytes, 1, 100 * 1024 * 1024))
        {
            return CreateArchiveFailure(operationName, $"Archive '{source.RelativePath}' exceeds the configured byte limit.", source.RelativePath, destination.RelativePath, startedAtUtc);
        }

        var targets = new List<(ZipArchiveEntry Entry, string TargetPath)>();
        var targetPaths = new HashSet<string>(pathPolicy.PhysicalPathComparer);
        foreach (var entry in entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(destination.FullPath, entry.FullName));
            if (!pathPolicy.IsPathWithinRoot(targetPath, destination.FullPath))
            {
                return CreateArchiveFailure(operationName, $"Archive entry '{entry.FullName}' escapes the destination directory.", source.RelativePath, destination.RelativePath, startedAtUtc);
            }

            if (!pathPolicy.TryValidateNoReparseTraversal(targetPath, out var reparseValidationMessage))
            {
                return CreateArchiveFailure(operationName, reparseValidationMessage, source.RelativePath, destination.RelativePath, startedAtUtc);
            }

            if (!targetPaths.Add(targetPath))
            {
                return CreateArchiveFailure(operationName, $"Archive contains duplicate target '{entry.FullName}'.", source.RelativePath, destination.RelativePath, startedAtUtc);
            }

            if (File.Exists(targetPath) && !overwrite)
            {
                return CreateArchiveFailure(operationName, $"Archive target '{pathPolicy.ToRelativePath(targetPath)}' already exists. Set overwrite to true to replace it.", source.RelativePath, destination.RelativePath, startedAtUtc);
            }

            if (Directory.Exists(targetPath))
            {
                return CreateArchiveFailure(operationName, $"Archive target '{pathPolicy.ToRelativePath(targetPath)}' is a directory.", source.RelativePath, destination.RelativePath, startedAtUtc);
            }

            targets.Add((entry, targetPath));
        }

        var placementCandidates = targets
            .Select(target => new WorkspaceDestinationContentCandidate(
                target.TargetPath,
                pathPolicy.ToRelativePath(target.TargetPath),
                File.Exists(target.TargetPath),
                () => ReadArchiveEntryText(target.Entry)))
            .ToArray();
        if (destinationContentPlacementPolicy.TryValidate(
                destination,
                destinationAuthorityRootPath,
                placementCandidates,
                out var placementMessage))
        {
            return CreateArchiveFailure(
                operationName,
                placementMessage,
                source.RelativePath,
                destination.RelativePath,
                startedAtUtc);
        }

        var destinationParent = Path.GetDirectoryName(destination.FullPath)
            ?? throw new InvalidOperationException($"Destination '{destination.FullPath}' has no containing directory.");
        pathPolicy.EnsureDirectoryForMutation(destinationParent);
        var stagingDirectory = WorkspaceFileMutationService.CreateSiblingArtifactPath(
            destination.FullPath,
            "stage");
        pathPolicy.ValidateMutationTarget(stagingDirectory);
        WorkspaceMutationCommitResult commitResult;
        try
        {
            if (Directory.Exists(destination.FullPath))
            {
                CopyDirectoryTree(destination.FullPath, stagingDirectory);
            }
            else
            {
                pathPolicy.EnsureDirectoryForMutation(stagingDirectory);
            }

            foreach (var (entry, targetPath) in targets)
            {
                var relativeTargetPath = Path.GetRelativePath(destination.FullPath, targetPath);
                var stagingTargetPath = Path.Combine(stagingDirectory, relativeTargetPath);
                var stagingTargetDirectory = Path.GetDirectoryName(stagingTargetPath);
                if (!string.IsNullOrWhiteSpace(stagingTargetDirectory))
                {
                    pathPolicy.EnsureDirectoryForMutation(stagingTargetDirectory);
                }

                pathPolicy.ValidateMutationTarget(stagingTargetPath);
                entry.ExtractToFile(stagingTargetPath, overwrite: true);
            }

            pathPolicy.ValidateMutationTarget(stagingDirectory);
            pathPolicy.ValidateMutationTarget(destination.FullPath);
            commitResult = WorkspaceFileMutationService.CommitStagedDirectory(
                stagingDirectory,
                destination.FullPath,
                overwrite: true);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                pathPolicy.ValidateMutationTarget(stagingDirectory);
                WorkspaceFileMutationService.DeleteDirectoryTree(stagingDirectory);
            }
        }

        var message = commitResult.AppendWarning(
            $"Extracted archive '{source.RelativePath}' to '{destination.RelativePath}'.");
        return new WorkspaceArchiveMutationResult(
            Succeeded: true,
            Message: message,
            Receipt: receiptWriter.CreateReceipt(operationName, true, "Succeeded", message, string.Empty, [source.RelativePath, destination.RelativePath], [], startedAtUtc),
            SourcePath: source.RelativePath,
            DestinationPath: destination.RelativePath,
            FileCount: entries.Length,
            TotalBytes: totalBytes,
            IsTruncated: false);
    }

    public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
        => queryService.DiffTextFiles(leftPath, rightPath, maxLines);

    private WorkspacePathHashResult CreateHashFailure(
        string operationName,
        string path,
        string pathKind,
        string message,
        DateTimeOffset startedAtUtc)
        => new(
            Succeeded: false,
            Message: message,
            Receipt: receiptWriter.CreateReceipt(operationName, false, "Failed", message, string.Empty, string.IsNullOrWhiteSpace(path) ? [] : [path], [], startedAtUtc),
            Path: path,
            PathKind: pathKind,
            Algorithm: "SHA-256",
            Hash: string.Empty,
            SizeBytes: 0,
            FileCount: 0,
            IsTruncated: false);

    private WorkspaceArchiveMutationResult CreateArchiveFailure(
        string operationName,
        string message,
        string sourcePath,
        string destinationPath,
        DateTimeOffset startedAtUtc)
        => new(
            Succeeded: false,
            Message: message,
            Receipt: receiptWriter.CreateReceipt(operationName, true, "Failed", message, string.Empty, BuildTargetPathList(sourcePath, destinationPath), [], startedAtUtc),
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
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                })
            .OrderBy(
                file => WorkspacePathPolicy.NormalizeRelativePath(
                    Path.GetRelativePath(source.FullPath, file)),
                StringComparer.Ordinal)
            .ThenBy(file => file, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadArchiveEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        return reader.ReadToEnd();
    }

    private void CopyDirectoryTree(string sourcePath, string destinationPath)
    {
        pathPolicy.ValidatePathForUse(sourcePath);
        pathPolicy.EnsureDirectoryForMutation(destinationPath);
        foreach (var directory in Directory.EnumerateDirectories(
                     sourcePath,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = false,
                         AttributesToSkip = FileAttributes.ReparsePoint
                     })
                 .OrderBy(
                     directory => WorkspacePathPolicy.NormalizeRelativePath(
                         Path.GetRelativePath(sourcePath, directory)),
                     StringComparer.Ordinal)
                 .ThenBy(directory => directory, StringComparer.Ordinal))
        {
            pathPolicy.EnsureDirectoryForMutation(
                Path.Combine(destinationPath, Path.GetRelativePath(sourcePath, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourcePath,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = false,
                         AttributesToSkip = FileAttributes.ReparsePoint
                     })
                 .OrderBy(
                     file => WorkspacePathPolicy.NormalizeRelativePath(
                         Path.GetRelativePath(sourcePath, file)),
                     StringComparer.Ordinal)
                 .ThenBy(file => file, StringComparer.Ordinal))
        {
            var targetPath = Path.Combine(
                destinationPath,
                Path.GetRelativePath(sourcePath, file));
            pathPolicy.ValidatePathForUse(file);
            pathPolicy.EnsureParentDirectoryForMutation(targetPath);
            pathPolicy.ValidateMutationTarget(targetPath);
            File.Copy(file, targetPath, overwrite: false);
        }
    }

    private static string NormalizeArchiveEntryPath(string path)
        => path.Replace('\\', '/');

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

    private static IReadOnlyList<string> BuildTargetPathList(string? sourcePath, string? destinationPath)
    {
        return new[] { sourcePath, destinationPath }
            .OfType<string>()
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToList();
    }
}
