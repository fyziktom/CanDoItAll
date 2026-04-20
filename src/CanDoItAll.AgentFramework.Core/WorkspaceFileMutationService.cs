using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceFileMutationService
{
    private readonly WorkspacePathPolicy pathPolicy;
    private readonly WorkspaceFileReceiptWriter receiptWriter;

    public WorkspaceFileMutationService(WorkspacePathPolicy pathPolicy, WorkspaceFileReceiptWriter receiptWriter)
    {
        this.pathPolicy = pathPolicy;
        this.receiptWriter = receiptWriter;
    }

    public WorkspaceFileMutationResult CreateDirectory(string path)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return CreateMutationFailure("workspace_create_directory", validationMessage, string.Empty, null, "directory", startedAtUtc);
        }

        if (File.Exists(resolution.FullPath))
        {
            return CreateMutationFailure(
                "workspace_create_directory",
                $"Cannot create directory '{resolution.RelativePath}' because a file already exists at that path.",
                resolution.RelativePath,
                null,
                "directory",
                startedAtUtc);
        }

        var existedBefore = Directory.Exists(resolution.FullPath);
        Directory.CreateDirectory(resolution.FullPath);
        var message = existedBefore
            ? $"Directory '{resolution.RelativePath}' already existed."
            : $"Created directory '{resolution.RelativePath}'.";

        return CreateMutationSuccess(
            operation: "workspace_create_directory",
            message: message,
            path: resolution.RelativePath,
            destinationPath: null,
            pathKind: "directory",
            pathExistedBefore: existedBefore,
            createdNewPath: !existedBefore,
            overwroteExistingPath: false,
            characterCount: 0,
            targetPaths: [resolution.RelativePath],
            startedAtUtc: startedAtUtc);
    }

    public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return CreateMutationFailure("workspace_write_file", validationMessage, string.Empty, null, "file", startedAtUtc);
        }

        if (Directory.Exists(resolution.FullPath))
        {
            return CreateMutationFailure(
                "workspace_write_file",
                $"Cannot write '{resolution.RelativePath}' because the target path is a directory.",
                resolution.RelativePath,
                null,
                "file",
                startedAtUtc);
        }

        var existedBefore = File.Exists(resolution.FullPath);
        if (existedBefore && !overwrite)
        {
            return CreateMutationFailure(
                "workspace_write_file",
                $"File '{resolution.RelativePath}' already exists. Set overwrite to true to replace it.",
                resolution.RelativePath,
                null,
                "file",
                startedAtUtc);
        }

        var directory = Path.GetDirectoryName(resolution.FullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var safeContent = content ?? string.Empty;
        File.WriteAllText(resolution.FullPath, safeContent);
        var message = existedBefore
            ? $"Overwrote '{resolution.RelativePath}' with {safeContent.Length} characters."
            : $"Created '{resolution.RelativePath}' with {safeContent.Length} characters.";

        return CreateMutationSuccess(
            operation: "workspace_write_file",
            message: message,
            path: resolution.RelativePath,
            destinationPath: null,
            pathKind: "file",
            pathExistedBefore: existedBefore,
            createdNewPath: !existedBefore,
            overwroteExistingPath: existedBefore,
            characterCount: safeContent.Length,
            targetPaths: [resolution.RelativePath],
            startedAtUtc: startedAtUtc);
    }

    public WorkspaceFileMutationResult AppendTextFile(string path, string content)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return CreateMutationFailure("workspace_append_file", validationMessage, string.Empty, null, "file", startedAtUtc);
        }

        if (Directory.Exists(resolution.FullPath))
        {
            return CreateMutationFailure(
                "workspace_append_file",
                $"Cannot append to '{resolution.RelativePath}' because the target path is a directory.",
                resolution.RelativePath,
                null,
                "file",
                startedAtUtc);
        }

        var directory = Path.GetDirectoryName(resolution.FullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var existedBefore = File.Exists(resolution.FullPath);
        var safeContent = content ?? string.Empty;
        File.AppendAllText(resolution.FullPath, safeContent);
        var message = existedBefore
            ? $"Appended {safeContent.Length} characters to '{resolution.RelativePath}'."
            : $"Created '{resolution.RelativePath}' and appended {safeContent.Length} characters.";

        return CreateMutationSuccess(
            operation: "workspace_append_file",
            message: message,
            path: resolution.RelativePath,
            destinationPath: null,
            pathKind: "file",
            pathExistedBefore: existedBefore,
            createdNewPath: !existedBefore,
            overwroteExistingPath: false,
            characterCount: safeContent.Length,
            targetPaths: [resolution.RelativePath],
            startedAtUtc: startedAtUtc);
    }

    public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(sourcePath, allowWorkspaceRoot: false, out var sourceResolution, out var sourceValidation))
        {
            return CreateMutationFailure("workspace_copy_path", sourceValidation, string.Empty, string.Empty, "missing", startedAtUtc);
        }

        if (!pathPolicy.TryResolveWorkspacePath(destinationPath, allowWorkspaceRoot: false, out var destinationResolution, out var destinationValidation))
        {
            return CreateMutationFailure("workspace_copy_path", destinationValidation, sourceResolution.RelativePath, string.Empty, ResolvePathKind(sourceResolution.FullPath), startedAtUtc);
        }

        if (string.Equals(sourceResolution.FullPath, destinationResolution.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            return CreateMutationFailure(
                "workspace_copy_path",
                "Source and destination paths must be different for copy operations.",
                sourceResolution.RelativePath,
                destinationResolution.RelativePath,
                ResolvePathKind(sourceResolution.FullPath),
                startedAtUtc);
        }

        if (File.Exists(sourceResolution.FullPath))
        {
            if (Directory.Exists(destinationResolution.FullPath))
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    $"Cannot copy file '{sourceResolution.RelativePath}' onto existing directory '{destinationResolution.RelativePath}'.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "file",
                    startedAtUtc);
            }

            var destinationDirectory = Path.GetDirectoryName(destinationResolution.FullPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var existedBefore = File.Exists(destinationResolution.FullPath);
            if (existedBefore && !overwrite)
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    $"Destination file '{destinationResolution.RelativePath}' already exists. Set overwrite to true to replace it.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "file",
                    startedAtUtc);
            }

            File.Copy(sourceResolution.FullPath, destinationResolution.FullPath, overwrite);
            var message = existedBefore
                ? $"Copied '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' and replaced the previous file."
                : $"Copied '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}'.";

            return CreateMutationSuccess(
                operation: "workspace_copy_path",
                message: message,
                path: sourceResolution.RelativePath,
                destinationPath: destinationResolution.RelativePath,
                pathKind: "file",
                pathExistedBefore: existedBefore,
                createdNewPath: !existedBefore,
                overwroteExistingPath: existedBefore,
                characterCount: 0,
                targetPaths: [sourceResolution.RelativePath, destinationResolution.RelativePath],
                startedAtUtc: startedAtUtc);
        }

        if (Directory.Exists(sourceResolution.FullPath))
        {
            if (File.Exists(destinationResolution.FullPath))
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    $"Cannot copy directory '{sourceResolution.RelativePath}' onto existing file '{destinationResolution.RelativePath}'.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            if (IsNestedPath(sourceResolution.FullPath, destinationResolution.FullPath))
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    $"Cannot copy directory '{sourceResolution.RelativePath}' into one of its own descendants.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            var existedBefore = Directory.Exists(destinationResolution.FullPath);
            if (existedBefore && !overwrite)
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    $"Destination directory '{destinationResolution.RelativePath}' already exists. Set overwrite to true to replace it.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            if (existedBefore && overwrite)
            {
                Directory.Delete(destinationResolution.FullPath, recursive: true);
            }

            CopyDirectory(sourceResolution.FullPath, destinationResolution.FullPath, overwrite: true);
            var message = existedBefore
                ? $"Copied directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' and replaced the previous directory."
                : $"Copied directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}'.";

            return CreateMutationSuccess(
                operation: "workspace_copy_path",
                message: message,
                path: sourceResolution.RelativePath,
                destinationPath: destinationResolution.RelativePath,
                pathKind: "directory",
                pathExistedBefore: existedBefore,
                createdNewPath: !existedBefore,
                overwroteExistingPath: existedBefore,
                characterCount: 0,
                targetPaths: [sourceResolution.RelativePath, destinationResolution.RelativePath],
                startedAtUtc: startedAtUtc);
        }

        return CreateMutationFailure(
            "workspace_copy_path",
            $"Source path '{sourceResolution.RelativePath}' does not exist in the workspace.",
            sourceResolution.RelativePath,
            destinationResolution.RelativePath,
            "missing",
            startedAtUtc);
    }

    public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(sourcePath, allowWorkspaceRoot: false, out var sourceResolution, out var sourceValidation))
        {
            return CreateMutationFailure("workspace_move_path", sourceValidation, string.Empty, string.Empty, "missing", startedAtUtc);
        }

        if (!pathPolicy.TryResolveWorkspacePath(destinationPath, allowWorkspaceRoot: false, out var destinationResolution, out var destinationValidation))
        {
            return CreateMutationFailure("workspace_move_path", destinationValidation, sourceResolution.RelativePath, string.Empty, ResolvePathKind(sourceResolution.FullPath), startedAtUtc);
        }

        if (string.Equals(sourceResolution.FullPath, destinationResolution.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            return CreateMutationFailure(
                "workspace_move_path",
                "Source and destination paths must be different for move operations.",
                sourceResolution.RelativePath,
                destinationResolution.RelativePath,
                ResolvePathKind(sourceResolution.FullPath),
                startedAtUtc);
        }

        if (File.Exists(sourceResolution.FullPath))
        {
            if (Directory.Exists(destinationResolution.FullPath))
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    $"Cannot move file '{sourceResolution.RelativePath}' onto existing directory '{destinationResolution.RelativePath}'.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "file",
                    startedAtUtc);
            }

            var destinationDirectory = Path.GetDirectoryName(destinationResolution.FullPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var existedBefore = File.Exists(destinationResolution.FullPath);
            if (existedBefore && !overwrite)
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    $"Destination file '{destinationResolution.RelativePath}' already exists. Set overwrite to true to replace it.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "file",
                    startedAtUtc);
            }

            File.Move(sourceResolution.FullPath, destinationResolution.FullPath, overwrite);
            var message = existedBefore
                ? $"Moved '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' and replaced the previous file."
                : $"Moved '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}'.";

            return CreateMutationSuccess(
                operation: "workspace_move_path",
                message: message,
                path: sourceResolution.RelativePath,
                destinationPath: destinationResolution.RelativePath,
                pathKind: "file",
                pathExistedBefore: existedBefore,
                createdNewPath: !existedBefore,
                overwroteExistingPath: existedBefore,
                characterCount: 0,
                targetPaths: [sourceResolution.RelativePath, destinationResolution.RelativePath],
                startedAtUtc: startedAtUtc);
        }

        if (Directory.Exists(sourceResolution.FullPath))
        {
            if (File.Exists(destinationResolution.FullPath))
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    $"Cannot move directory '{sourceResolution.RelativePath}' onto existing file '{destinationResolution.RelativePath}'.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            if (IsNestedPath(sourceResolution.FullPath, destinationResolution.FullPath))
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    $"Cannot move directory '{sourceResolution.RelativePath}' into one of its own descendants.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            var existedBefore = Directory.Exists(destinationResolution.FullPath);
            if (existedBefore && !overwrite)
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    $"Destination directory '{destinationResolution.RelativePath}' already exists. Set overwrite to true to replace it.",
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            if (existedBefore && overwrite)
            {
                Directory.Delete(destinationResolution.FullPath, recursive: true);
            }

            var destinationDirectory = Path.GetDirectoryName(destinationResolution.FullPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            Directory.Move(sourceResolution.FullPath, destinationResolution.FullPath);
            var message = existedBefore
                ? $"Moved directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' and replaced the previous directory."
                : $"Moved directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}'.";

            return CreateMutationSuccess(
                operation: "workspace_move_path",
                message: message,
                path: sourceResolution.RelativePath,
                destinationPath: destinationResolution.RelativePath,
                pathKind: "directory",
                pathExistedBefore: existedBefore,
                createdNewPath: !existedBefore,
                overwroteExistingPath: existedBefore,
                characterCount: 0,
                targetPaths: [sourceResolution.RelativePath, destinationResolution.RelativePath],
                startedAtUtc: startedAtUtc);
        }

        return CreateMutationFailure(
            "workspace_move_path",
            $"Source path '{sourceResolution.RelativePath}' does not exist in the workspace.",
            sourceResolution.RelativePath,
            destinationResolution.RelativePath,
            "missing",
            startedAtUtc);
    }

    public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return CreateMutationFailure("workspace_delete_path", validationMessage, string.Empty, null, "missing", startedAtUtc);
        }

        if (File.Exists(resolution.FullPath))
        {
            File.Delete(resolution.FullPath);
            var message = $"Deleted file '{resolution.RelativePath}'.";
            return CreateMutationSuccess(
                operation: "workspace_delete_path",
                message: message,
                path: resolution.RelativePath,
                destinationPath: null,
                pathKind: "file",
                pathExistedBefore: true,
                createdNewPath: false,
                overwroteExistingPath: false,
                characterCount: 0,
                targetPaths: [resolution.RelativePath],
                startedAtUtc: startedAtUtc);
        }

        if (Directory.Exists(resolution.FullPath))
        {
            if (!recursive && Directory.EnumerateFileSystemEntries(resolution.FullPath).Any())
            {
                return CreateMutationFailure(
                    "workspace_delete_path",
                    $"Directory '{resolution.RelativePath}' is not empty. Set recursive to true to remove it.",
                    resolution.RelativePath,
                    null,
                    "directory",
                    startedAtUtc);
            }

            Directory.Delete(resolution.FullPath, recursive);
            var message = recursive
                ? $"Deleted directory '{resolution.RelativePath}' recursively."
                : $"Deleted empty directory '{resolution.RelativePath}'.";

            return CreateMutationSuccess(
                operation: "workspace_delete_path",
                message: message,
                path: resolution.RelativePath,
                destinationPath: null,
                pathKind: "directory",
                pathExistedBefore: true,
                createdNewPath: false,
                overwroteExistingPath: false,
                characterCount: 0,
                targetPaths: [resolution.RelativePath],
                startedAtUtc: startedAtUtc);
        }

        return CreateMutationFailure(
            "workspace_delete_path",
            $"Path '{resolution.RelativePath}' does not exist in the workspace.",
            resolution.RelativePath,
            null,
            "missing",
            startedAtUtc);
    }

    private WorkspaceFileMutationResult CreateMutationSuccess(
        string operation,
        string message,
        string path,
        string? destinationPath,
        string pathKind,
        bool pathExistedBefore,
        bool createdNewPath,
        bool overwroteExistingPath,
        int characterCount,
        IReadOnlyList<string> targetPaths,
        DateTimeOffset startedAtUtc)
    {
        var targetArtifacts = receiptWriter.BuildTargetArtifactReferences(targetPaths, operation);
        var receipt = receiptWriter.WriteMutationReceipt(operation, message, targetPaths, targetArtifacts, startedAtUtc);
        return new WorkspaceFileMutationResult(
            Succeeded: true,
            Message: message,
            Receipt: receipt,
            Path: path,
            DestinationPath: destinationPath,
            PathKind: pathKind,
            PathExistedBefore: pathExistedBefore,
            CreatedNewPath: createdNewPath,
            OverwroteExistingPath: overwroteExistingPath,
            CharacterCount: characterCount);
    }

    private WorkspaceFileMutationResult CreateMutationFailure(
        string operation,
        string message,
        string path,
        string? destinationPath,
        string pathKind,
        DateTimeOffset startedAtUtc)
    {
        return new WorkspaceFileMutationResult(
            Succeeded: false,
            Message: message,
            Receipt: receiptWriter.CreateReceipt(operation, true, "Failed", message, string.Empty, BuildTargetPathList(path, destinationPath), [], startedAtUtc),
            Path: path,
            DestinationPath: destinationPath,
            PathKind: pathKind,
            PathExistedBefore: false,
            CreatedNewPath: false,
            OverwroteExistingPath: false,
            CharacterCount: 0);
    }

    private static string ResolvePathKind(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return "file";
        }

        if (Directory.Exists(fullPath))
        {
            return "directory";
        }

        return "missing";
    }

    private static IReadOnlyList<string> BuildTargetPathList(string? path, string? destinationPath)
    {
        return new[] { path, destinationPath }
            .OfType<string>()
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CopyDirectory(string sourcePath, string destinationPath, bool overwrite)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var directory in Directory.EnumerateDirectories(
                     sourcePath,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = true,
                         AttributesToSkip = 0
                     }))
        {
            var relativePath = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourcePath,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = true,
                         AttributesToSkip = 0
                     }))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var destinationFile = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite);
        }
    }

    private static bool IsNestedPath(string parentPath, string candidatePath)
    {
        var normalizedParent = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
        var normalizedCandidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
        return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
