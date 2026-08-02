using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal readonly record struct WorkspaceMutationCommitResult(
    string RetainedCleanupArtifact)
{
    public bool HasCleanupWarning => !string.IsNullOrWhiteSpace(RetainedCleanupArtifact);

    public string AppendWarning(string message)
        => HasCleanupWarning
            ? $"{message} The mutation committed successfully, but transaction cleanup was incomplete; retained cleanup artifact '{RetainedCleanupArtifact}'."
            : message;
}

internal enum WorkspaceStagedFileCommitState
{
    NotCommitted,
    Committed
}

internal sealed record WorkspaceStagedFileCommitRequest(
    string StagingPath,
    string DestinationPath,
    string? BackupPath,
    bool ReplacesExistingFile);

internal sealed class WorkspaceStagedFileCommitAttempt
{
    private WorkspaceStagedFileCommitAttempt(
        WorkspaceStagedFileCommitState state,
        Exception? failure)
    {
        State = state;
        Failure = failure;
    }

    public WorkspaceStagedFileCommitState State { get; }

    public Exception? Failure { get; }

    public static WorkspaceStagedFileCommitAttempt Committed()
        => new(WorkspaceStagedFileCommitState.Committed, null);

    public static WorkspaceStagedFileCommitAttempt NotCommitted(Exception failure)
        => new(
            WorkspaceStagedFileCommitState.NotCommitted,
            failure ?? throw new ArgumentNullException(nameof(failure)));
}

internal sealed class WorkspaceFileMutationService
{
    private readonly record struct WorkspaceFileFingerprint(
        long Length,
        string Sha256);

    private static readonly HashSet<string> ProjectFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".fsproj",
        ".sln",
        ".slnx",
        ".vbproj"
    };

    private readonly WorkspacePathPolicy pathPolicy;
    private readonly WorkspaceFileReceiptWriter receiptWriter;
    private readonly WorkspaceDestinationContentPlacementPolicy destinationContentPlacementPolicy;
    private readonly Action<string> deleteDirectoryTree;
    private readonly Func<WorkspaceStagedFileCommitRequest, WorkspaceStagedFileCommitAttempt> commitStagedFile;
    private readonly Func<string, string?> resolveVolumeRoot;

    public WorkspaceFileMutationService(WorkspacePathPolicy pathPolicy, WorkspaceFileReceiptWriter receiptWriter)
        : this(
            pathPolicy,
            receiptWriter,
            new WorkspaceDestinationContentPlacementPolicy(pathPolicy),
            DeleteDirectoryTree,
            CommitStagedFileWithFileSystem,
            ResolveVolumeRoot)
    {
    }

    internal WorkspaceFileMutationService(
        WorkspacePathPolicy pathPolicy,
        WorkspaceFileReceiptWriter receiptWriter,
        Func<string, IReadOnlyList<string>> enumerateProjectFiles)
        : this(
            pathPolicy,
            receiptWriter,
            new WorkspaceDestinationContentPlacementPolicy(pathPolicy, enumerateProjectFiles),
            DeleteDirectoryTree,
            CommitStagedFileWithFileSystem,
            ResolveVolumeRoot)
    {
    }

    internal WorkspaceFileMutationService(
        WorkspacePathPolicy pathPolicy,
        WorkspaceFileReceiptWriter receiptWriter,
        Func<string, IReadOnlyList<string>> enumerateProjectFiles,
        Action<string> deleteDirectoryTree,
        Func<WorkspaceStagedFileCommitRequest, WorkspaceStagedFileCommitAttempt>? commitStagedFile = null,
        Func<string, string?>? resolveVolumeRoot = null)
        : this(
            pathPolicy,
            receiptWriter,
            new WorkspaceDestinationContentPlacementPolicy(pathPolicy, enumerateProjectFiles),
            deleteDirectoryTree,
            commitStagedFile,
            resolveVolumeRoot)
    {
    }

    internal WorkspaceFileMutationService(
        WorkspacePathPolicy pathPolicy,
        WorkspaceFileReceiptWriter receiptWriter,
        WorkspaceDestinationContentPlacementPolicy destinationContentPlacementPolicy,
        Action<string>? deleteDirectoryTree = null,
        Func<WorkspaceStagedFileCommitRequest, WorkspaceStagedFileCommitAttempt>? commitStagedFile = null,
        Func<string, string?>? resolveVolumeRoot = null)
    {
        this.pathPolicy = pathPolicy ?? throw new ArgumentNullException(nameof(pathPolicy));
        this.receiptWriter = receiptWriter ?? throw new ArgumentNullException(nameof(receiptWriter));
        this.destinationContentPlacementPolicy = destinationContentPlacementPolicy ?? throw new ArgumentNullException(nameof(destinationContentPlacementPolicy));
        this.deleteDirectoryTree = deleteDirectoryTree ?? DeleteDirectoryTree;
        this.commitStagedFile = commitStagedFile ?? CommitStagedFileWithFileSystem;
        this.resolveVolumeRoot = resolveVolumeRoot ?? ResolveVolumeRoot;
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

        if (ProjectFileExtensions.Contains(Path.GetExtension(resolution.FullPath)))
        {
            return CreateMutationFailure(
                "workspace_create_directory",
                $"Cannot create directory '{resolution.RelativePath}' because the path ends with a project-file extension. Project paths such as `.csproj` must be files; create the containing directory instead.",
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

    public WorkspaceFileMutationResult WriteTextFile(
        string path,
        string content,
        bool overwrite = true,
        string? authorityRootPath = null)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return CreateMutationFailure("workspace_write_file", validationMessage, string.Empty, null, "file", startedAtUtc);
        }

        if (Directory.Exists(resolution.FullPath))
        {
            var directoryNamedProjectFileHint = ProjectFileExtensions.Contains(Path.GetExtension(resolution.FullPath))
                ? " This is a directory named like a project file; do not retry the same write. Use the project container directory and create or repair an actual project file path instead."
                : string.Empty;

            return CreateMutationFailure(
                "workspace_write_file",
                $"Cannot write '{resolution.RelativePath}' because the target path is a directory.{directoryNamedProjectFileHint}",
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

        var safeContent = content ?? string.Empty;
        var contentByteCount = Encoding.UTF8.GetByteCount(safeContent);
        if (contentByteCount > WorkspaceFileLimits.MaxTextMutationBytes)
        {
            return CreateMutationFailure(
                "workspace_write_file",
                $"Cannot write '{resolution.RelativePath}' because its UTF-8 content exceeds the {WorkspaceFileLimits.MaxTextMutationBytes} byte text-mutation limit. No content changed.",
                resolution.RelativePath,
                null,
                "file",
                startedAtUtc);
        }

        if (destinationContentPlacementPolicy.TryValidate(
                resolution,
                authorityRootPath,
                [new WorkspaceDestinationContentCandidate(
                    resolution.FullPath,
                    resolution.RelativePath,
                    existedBefore,
                    () => safeContent)],
                out var placementMessage))
        {
            return CreateMutationFailure(
                "workspace_write_file",
                placementMessage,
                resolution.RelativePath,
                null,
                "file",
                startedAtUtc);
        }

        var commitResult = WriteTextFileAtomically(
            resolution.FullPath,
            safeContent,
            existedBefore,
            overwrite);
        var message = existedBefore
            ? $"Overwrote '{resolution.RelativePath}' with {safeContent.Length} characters."
            : $"Created '{resolution.RelativePath}' with {safeContent.Length} characters.";
        message = commitResult.AppendWarning(message);

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

    public WorkspaceFileMutationResult AppendTextFile(
        string path,
        string content,
        string? authorityRootPath = null)
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

        var existedBefore = File.Exists(resolution.FullPath);
        var safeContent = content ?? string.Empty;
        var appendedBytes = Encoding.UTF8.GetBytes(safeContent);
        if (appendedBytes.Length > WorkspaceFileLimits.MaxTextMutationBytes)
        {
            return CreateMutationFailure(
                "workspace_append_file",
                $"Cannot append to '{resolution.RelativePath}' because the appended UTF-8 content exceeds the {WorkspaceFileLimits.MaxTextMutationBytes} byte text-mutation limit. No content changed.",
                resolution.RelativePath,
                null,
                "file",
                startedAtUtc);
        }

        var directory = Path.GetDirectoryName(resolution.FullPath)
            ?? throw new InvalidOperationException($"Destination '{resolution.FullPath}' has no containing directory.");
        Directory.CreateDirectory(directory);
        var stagingPath = CreateSiblingArtifactPath(resolution.FullPath, "stage");
        try
        {
            if (!TryStageAppend(
                    resolution.FullPath,
                    stagingPath,
                    appendedBytes,
                    existedBefore,
                    out var originalFingerprint))
            {
                return CreateMutationFailure(
                    "workspace_append_file",
                    $"Cannot append to '{resolution.RelativePath}' because the resulting file would exceed the {WorkspaceFileLimits.MaxTextMutationBytes} byte text-mutation limit. No content changed.",
                    resolution.RelativePath,
                    null,
                    "file",
                    startedAtUtc);
            }

            if (destinationContentPlacementPolicy.TryValidate(
                    resolution,
                    authorityRootPath,
                    [new WorkspaceDestinationContentCandidate(
                        resolution.FullPath,
                        resolution.RelativePath,
                        existedBefore,
                        () => ReadBoundedStagedText(stagingPath))],
                    out var placementMessage))
            {
                return CreateMutationFailure(
                    "workspace_append_file",
                    placementMessage,
                    resolution.RelativePath,
                    null,
                    "file",
                    startedAtUtc);
            }

            if (existedBefore &&
                (!TryCaptureFingerprint(
                     resolution.FullPath,
                     WorkspaceFileLimits.MaxTextMutationBytes,
                     out var currentFingerprint) ||
                 currentFingerprint != originalFingerprint))
            {
                return CreateMutationFailure(
                    "workspace_append_file",
                    $"Cannot append to '{resolution.RelativePath}' because the file could not be verified unchanged while the append was being prepared. It may have changed or become temporarily inaccessible; retry against the current file. No content changed by this operation.",
                    resolution.RelativePath,
                    null,
                    "file",
                    startedAtUtc);
            }

            var commitResult = CommitStagedFile(
                stagingPath,
                resolution.FullPath,
                destinationExistedBefore: existedBefore,
                overwrite: existedBefore,
                commitStagedFile);
            var message = existedBefore
                ? $"Appended {safeContent.Length} characters to '{resolution.RelativePath}'."
                : $"Created '{resolution.RelativePath}' and appended {safeContent.Length} characters.";
            message = commitResult.AppendWarning(message);

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
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    public WorkspaceFileMutationResult CopyPath(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        string? destinationAuthorityRootPath = null)
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

            if (destinationContentPlacementPolicy.TryValidate(
                    destinationResolution,
                    destinationAuthorityRootPath,
                    [CreateDestinationCandidate(
                        sourceResolution.FullPath,
                        destinationResolution.FullPath,
                        destinationResolution.RelativePath)],
                    out var placementMessage))
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    placementMessage,
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "file",
                    startedAtUtc);
            }

            var commitResult = CopyFileAtomically(
                sourceResolution.FullPath,
                destinationResolution.FullPath,
                existedBefore,
                overwrite);
            var message = existedBefore
                ? $"Copied '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' and replaced the previous file."
                : $"Copied '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}'.";
            message = commitResult.AppendWarning(message);

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

            if (ProjectFileExtensions.Contains(Path.GetExtension(destinationResolution.FullPath)))
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    $"Cannot copy directory '{sourceResolution.RelativePath}' to project-file path '{destinationResolution.RelativePath}'. That would create a directory named like a `.csproj`; copy the actual project file or scaffold from the parent container instead.",
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

            var destinationCandidates = CreateDirectoryDestinationCandidates(
                sourceResolution.FullPath,
                destinationResolution.FullPath,
                destinationResolution.RelativePath);
            if (destinationContentPlacementPolicy.TryValidate(
                    destinationResolution,
                    destinationAuthorityRootPath,
                    destinationCandidates,
                    out var placementMessage))
            {
                return CreateMutationFailure(
                    "workspace_copy_path",
                    placementMessage,
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            var commitResult = CopyDirectoryAtomically(
                sourceResolution.FullPath,
                destinationResolution.FullPath,
                overwrite);
            var message = existedBefore
                ? $"Copied directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' and replaced the previous directory."
                : $"Copied directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}'.";
            message = commitResult.AppendWarning(message);

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

    public WorkspaceFileMutationResult MovePath(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        string? destinationAuthorityRootPath = null)
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

        var sourcePathKind = ResolvePathKind(sourceResolution.FullPath);
        if (sourcePathKind != "missing" &&
            !ArePathsOnSameVolume(
                sourceResolution.FullPath,
                destinationResolution.FullPath,
                resolveVolumeRoot))
        {
            return CreateMutationFailure(
                "workspace_move_path",
                $"Cannot move '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' across filesystem volumes. Copy it with workspace_copy_path, verify the destination, and only then remove the source with workspace_delete_path. No content changed.",
                sourceResolution.RelativePath,
                destinationResolution.RelativePath,
                sourcePathKind,
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

            if (destinationContentPlacementPolicy.TryValidate(
                    destinationResolution,
                    destinationAuthorityRootPath,
                    [CreateDestinationCandidate(
                        sourceResolution.FullPath,
                        destinationResolution.FullPath,
                        destinationResolution.RelativePath)],
                    out var placementMessage))
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    placementMessage,
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

            if (ProjectFileExtensions.Contains(Path.GetExtension(destinationResolution.FullPath)))
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    $"Cannot move directory '{sourceResolution.RelativePath}' to project-file path '{destinationResolution.RelativePath}'. That would create a directory named like a `.csproj`; move the actual project file or scaffold from the parent container instead.",
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

            var destinationCandidates = CreateDirectoryDestinationCandidates(
                sourceResolution.FullPath,
                destinationResolution.FullPath,
                destinationResolution.RelativePath);
            if (destinationContentPlacementPolicy.TryValidate(
                    destinationResolution,
                    destinationAuthorityRootPath,
                    destinationCandidates,
                    out var placementMessage))
            {
                return CreateMutationFailure(
                    "workspace_move_path",
                    placementMessage,
                    sourceResolution.RelativePath,
                    destinationResolution.RelativePath,
                    "directory",
                    startedAtUtc);
            }

            var destinationDirectory = Path.GetDirectoryName(destinationResolution.FullPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var commitResult = MoveDirectoryWithRollback(
                sourceResolution.FullPath,
                destinationResolution.FullPath,
                overwrite);
            var message = existedBefore
                ? $"Moved directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}' and replaced the previous directory."
                : $"Moved directory '{sourceResolution.RelativePath}' to '{destinationResolution.RelativePath}'.";
            message = commitResult.AppendWarning(message);

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
            if (ProjectFileExtensions.Contains(Path.GetExtension(resolution.FullPath)))
            {
                return CreateMutationFailure(
                    "workspace_delete_path",
                    $"Deleting project or solution file '{resolution.RelativePath}' is not allowed. Edit the project file or repair the existing project in place.",
                    resolution.RelativePath,
                    null,
                    "file",
                    startedAtUtc);
            }

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
            if (recursive && ContainsProjectFile(resolution.FullPath))
            {
                return CreateMutationFailure(
                    "workspace_delete_path",
                    $"Recursive delete is not allowed for project directory '{resolution.RelativePath}' because it contains a .NET project or solution file. Repair the existing project in place, or delete specific stale files and empty folders instead.",
                    resolution.RelativePath,
                    null,
                    "directory",
                    startedAtUtc);
            }

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

            WorkspaceMutationCommitResult commitResult = default;
            if (recursive)
            {
                commitResult = CommitRecursiveDirectoryDelete(
                    resolution.FullPath,
                    deleteDirectoryTree: deleteDirectoryTree);
            }
            else
            {
                Directory.Delete(resolution.FullPath, recursive: false);
            }

            var message = recursive
                ? $"Deleted directory '{resolution.RelativePath}' recursively."
                : $"Deleted empty directory '{resolution.RelativePath}'.";
            message = commitResult.AppendWarning(message);

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
                         IgnoreInaccessible = false,
                         AttributesToSkip = FileAttributes.ReparsePoint
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
                         IgnoreInaccessible = false,
                         AttributesToSkip = FileAttributes.ReparsePoint
                     }))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var destinationFile = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite);
        }
    }

    private static bool IsNestedPath(string parentPath, string candidatePath)
        => WorkspacePathPolicy.IsPathWithinRoot(candidatePath, parentPath);

    private static bool ContainsProjectFile(string directory)
        => Directory.EnumerateFiles(
                directory,
                "*.*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                })
            .Any(path => ProjectFileExtensions.Contains(Path.GetExtension(path)));

    private static WorkspaceDestinationContentCandidate CreateDestinationCandidate(
        string sourcePath,
        string destinationPath,
        string destinationDisplayPath)
        => new(
            destinationPath,
            destinationDisplayPath,
            File.Exists(destinationPath),
            () => File.ReadAllText(sourcePath));

    private static IReadOnlyList<WorkspaceDestinationContentCandidate> CreateDirectoryDestinationCandidates(
        string sourcePath,
        string destinationPath,
        string destinationDisplayPath)
        => Directory.EnumerateFiles(
                sourcePath,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                })
            .Select(sourceFile =>
            {
                var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
                var targetPath = Path.Combine(destinationPath, relativePath);
                var targetDisplayPath = Path.Combine(destinationDisplayPath, relativePath)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                return CreateDestinationCandidate(sourceFile, targetPath, targetDisplayPath);
            })
            .ToArray();

    private static bool TryStageAppend(
        string destinationPath,
        string stagingPath,
        byte[] appendedBytes,
        bool destinationExistedBefore,
        out WorkspaceFileFingerprint originalFingerprint)
    {
        originalFingerprint = default;
        using var stagingStream = new FileStream(
            stagingPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        if (destinationExistedBefore)
        {
            using var sourceStream = new FileStream(
                destinationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
            var existingByteLimit = WorkspaceFileLimits.MaxTextMutationBytes - appendedBytes.Length;
            if (!TryCopyAndFingerprint(
                    sourceStream,
                    stagingStream,
                    existingByteLimit,
                    out originalFingerprint))
            {
                return false;
            }
        }

        stagingStream.Write(appendedBytes, 0, appendedBytes.Length);
        stagingStream.Flush(flushToDisk: true);
        return true;
    }

    private static bool TryCaptureFingerprint(
        string path,
        long maxBytes,
        out WorkspaceFileFingerprint fingerprint)
    {
        fingerprint = default;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
            return TryCopyAndFingerprint(
                stream,
                destination: null,
                maxBytes,
                out fingerprint);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryCopyAndFingerprint(
        Stream source,
        Stream? destination,
        long maxBytes,
        out WorkspaceFileFingerprint fingerprint)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long totalBytes = 0;
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > maxBytes)
            {
                fingerprint = default;
                return false;
            }

            hash.AppendData(buffer, 0, read);
            destination?.Write(buffer, 0, read);
        }

        fingerprint = new WorkspaceFileFingerprint(
            totalBytes,
            Convert.ToHexString(hash.GetHashAndReset()));
        return true;
    }

    private static string ReadBoundedStagedText(string stagingPath)
    {
        using var stream = new FileStream(
            stagingPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        var buffer = new byte[WorkspaceFileLimits.MaxTextMutationBytes + 1];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead > WorkspaceFileLimits.MaxTextMutationBytes)
        {
            throw new IOException("The staged text mutation exceeded its verified byte limit.");
        }

        var content = Encoding.UTF8.GetString(buffer, 0, totalRead);
        return content.Length > 0 && content[0] == '\uFEFF'
            ? content[1..]
            : content;
    }

    private WorkspaceMutationCommitResult WriteTextFileAtomically(
        string destinationPath,
        string content,
        bool destinationExistedBefore,
        bool overwrite)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException($"Destination '{destinationPath}' has no containing directory.");
        Directory.CreateDirectory(destinationDirectory);
        var stagingPath = CreateSiblingArtifactPath(destinationPath, "stage");

        try
        {
            File.WriteAllText(stagingPath, content);
            return CommitStagedFile(
                stagingPath,
                destinationPath,
                destinationExistedBefore,
                overwrite,
                commitStagedFile);
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    private static WorkspaceMutationCommitResult CopyFileAtomically(
        string sourcePath,
        string destinationPath,
        bool destinationExistedBefore,
        bool overwrite)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException($"Destination '{destinationPath}' has no containing directory.");
        Directory.CreateDirectory(destinationDirectory);
        var stagingPath = CreateSiblingArtifactPath(destinationPath, "stage");

        try
        {
            File.Copy(sourcePath, stagingPath, overwrite: false);
            return CommitStagedFile(
                stagingPath,
                destinationPath,
                destinationExistedBefore,
                overwrite);
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    internal static WorkspaceMutationCommitResult CommitStagedFile(
        string stagingPath,
        string destinationPath,
        bool destinationExistedBefore,
        bool overwrite,
        Func<WorkspaceStagedFileCommitRequest, WorkspaceStagedFileCommitAttempt>? commitStagedFile = null,
        Action<string>? deleteFile = null)
    {
        var commitFile = commitStagedFile ?? CommitStagedFileWithFileSystem;
        var cleanupFile = deleteFile ?? File.Delete;
        if (destinationExistedBefore && !overwrite)
        {
            throw new IOException($"Destination file '{destinationPath}' already exists.");
        }

        var backupPath = destinationExistedBefore
            ? CreateSiblingArtifactPath(destinationPath, "backup")
            : null;
        var attempt = commitFile(new WorkspaceStagedFileCommitRequest(
            stagingPath,
            destinationPath,
            backupPath,
            ReplacesExistingFile: destinationExistedBefore));
        if (attempt.State == WorkspaceStagedFileCommitState.NotCommitted)
        {
            if (backupPath is not null && File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            throw attempt.Failure ?? new IOException("The staged-file commit did not complete.");
        }

        if (backupPath is null)
        {
            return default;
        }

        return CleanupCommittedArtifact(cleanupFile, backupPath);
    }

    private WorkspaceMutationCommitResult CopyDirectoryAtomically(
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException($"Destination '{destinationPath}' has no containing directory.");
        Directory.CreateDirectory(destinationDirectory);
        var stagingPath = CreateSiblingArtifactPath(destinationPath, "stage");

        try
        {
            CopyDirectory(sourcePath, stagingPath, overwrite: true);
            return CommitStagedDirectory(
                stagingPath,
                destinationPath,
                overwrite,
                deleteDirectoryTree);
        }
        finally
        {
            if (Directory.Exists(stagingPath))
            {
                deleteDirectoryTree(stagingPath);
            }
        }
    }

    private WorkspaceMutationCommitResult MoveDirectoryWithRollback(
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        if (!Directory.Exists(destinationPath))
        {
            Directory.Move(sourcePath, destinationPath);
            return default;
        }

        if (!overwrite)
        {
            throw new IOException($"Destination directory '{destinationPath}' already exists.");
        }

        var backupPath = CreateSiblingArtifactPath(destinationPath, "backup");
        Directory.Move(destinationPath, backupPath);
        try
        {
            Directory.Move(sourcePath, destinationPath);
        }
        catch
        {
            if (!Directory.Exists(destinationPath) && Directory.Exists(backupPath))
            {
                Directory.Move(backupPath, destinationPath);
            }

            throw;
        }

        return CleanupCommittedArtifact(deleteDirectoryTree, backupPath);
    }

    internal static WorkspaceMutationCommitResult CommitStagedDirectory(
        string stagingPath,
        string destinationPath,
        bool overwrite,
        Action<string>? deleteDirectoryTree = null)
    {
        if (!Directory.Exists(destinationPath))
        {
            Directory.Move(stagingPath, destinationPath);
            return default;
        }

        if (!overwrite)
        {
            throw new IOException($"Destination directory '{destinationPath}' already exists.");
        }

        var backupPath = CreateSiblingArtifactPath(destinationPath, "backup");
        Directory.Move(destinationPath, backupPath);
        try
        {
            Directory.Move(stagingPath, destinationPath);
        }
        catch
        {
            if (!Directory.Exists(destinationPath) && Directory.Exists(backupPath))
            {
                Directory.Move(backupPath, destinationPath);
            }

            throw;
        }

        return CleanupCommittedArtifact(
            deleteDirectoryTree ?? DeleteDirectoryTree,
            backupPath);
    }

    internal static WorkspaceMutationCommitResult CommitRecursiveDirectoryDelete(
        string directoryPath,
        Action<string, string>? moveDirectory = null,
        Action<string>? deleteDirectoryTree = null)
    {
        var tombstonePath = CreateSiblingArtifactPath(directoryPath, "tombstone");
        var move = moveDirectory ?? Directory.Move;
        try
        {
            move(directoryPath, tombstonePath);
        }
        catch
        {
            if (!Directory.Exists(directoryPath) && Directory.Exists(tombstonePath))
            {
                Directory.Move(tombstonePath, directoryPath);
            }

            throw;
        }

        return CleanupCommittedArtifact(
            deleteDirectoryTree ?? DeleteDirectoryTree,
            tombstonePath);
    }

    private static WorkspaceMutationCommitResult CleanupCommittedArtifact(
        Action<string> deleteArtifact,
        string artifactPath)
    {
        try
        {
            deleteArtifact(artifactPath);
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return new WorkspaceMutationCommitResult(Path.GetFileName(artifactPath));
        }
        catch (IOException)
        {
            return new WorkspaceMutationCommitResult(Path.GetFileName(artifactPath));
        }
    }

    internal static bool ArePathsOnSameVolume(
        string sourcePath,
        string destinationPath,
        Func<string, string?>? volumeRootResolver = null)
    {
        var resolver = volumeRootResolver ?? ResolveVolumeRoot;
        var sourceRoot = resolver(sourcePath);
        var destinationRoot = resolver(destinationPath);
        return !string.IsNullOrWhiteSpace(sourceRoot) &&
               !string.IsNullOrWhiteSpace(destinationRoot) &&
               string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveVolumeRoot(string path)
        => Path.GetPathRoot(Path.GetFullPath(path));

    private static WorkspaceStagedFileCommitAttempt CommitStagedFileWithFileSystem(
        WorkspaceStagedFileCommitRequest request)
    {
        try
        {
            if (request.ReplacesExistingFile)
            {
                File.Replace(
                    request.StagingPath,
                    request.DestinationPath,
                    request.BackupPath);
            }
            else
            {
                File.Move(
                    request.StagingPath,
                    request.DestinationPath,
                    overwrite: false);
            }

            return WorkspaceStagedFileCommitAttempt.Committed();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return WorkspaceStagedFileCommitAttempt.NotCommitted(exception);
        }
    }

    internal static string CreateSiblingArtifactPath(string destinationPath, string purpose)
    {
        var parentDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException($"Destination '{destinationPath}' has no containing directory.");
        var destinationName = Path.GetFileName(Path.TrimEndingDirectorySeparator(destinationPath));
        return Path.Combine(
            parentDirectory,
            $".{destinationName}.candoitall-{purpose}-{Guid.NewGuid():N}");
    }

    internal static void DeleteDirectoryTree(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(
                     path,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = false,
                         AttributesToSkip = FileAttributes.ReparsePoint
                     }))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

}
