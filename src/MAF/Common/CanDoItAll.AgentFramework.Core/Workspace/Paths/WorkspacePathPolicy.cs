using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Core;

internal readonly record struct WorkspacePathResolution(
    string FullPath,
    string RelativePath,
    string DisplayPath,
    bool IsWorkspacePath);

internal sealed class WorkspacePathPolicy
{
    private const string ExternalTargetAliasRoot = "external-target";

    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IExternalTargetPathRegistry? externalTargetRegistry;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;
    private readonly IPhysicalFileSystemPathPolicy workspacePathPolicy;

    public WorkspacePathPolicy(
        string workspaceRoot,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(workspaceRoot);
        this.physicalPathPolicyFactory = physicalPathPolicyFactory ?? throw new ArgumentNullException(nameof(physicalPathPolicyFactory));
        workspacePathPolicy = physicalPathPolicyFactory.Create(Path.GetFullPath(workspaceRoot));
        this.workspaceRoot = workspacePathPolicy.RootPath;
        this.workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        this.externalTargetRegistry = externalTargetRegistry;
    }

    public string WorkspaceRoot => workspaceRoot;

    public WorkspaceScopeDescriptor WorkspaceScope => workspaceScope;

    internal StringComparer PhysicalPathComparer => workspacePathPolicy.PathComparer;

    internal IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory => physicalPathPolicyFactory;

    public bool TryResolveWorkspacePath(string? path, bool allowWorkspaceRoot, out WorkspacePathResolution resolution, out string validationMessage)
    {
        resolution = CreateWorkspaceResolution(workspaceRoot);
        validationMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            if (allowWorkspaceRoot)
            {
                return TryValidateNoReparseTraversal(workspaceRoot, out validationMessage);
            }

            validationMessage = "Provide a workspace-relative path.";
            return false;
        }

        var externalAliasResolution = TryResolveExternalTargetAlias(path, out var externalResolution, out var externalValidationMessage);
        if (externalAliasResolution == ExternalTargetAliasResolution.Resolved)
        {
            if (!TryValidateNoReparseTraversal(externalResolution.FullPath, out validationMessage))
            {
                resolution = default;
                return false;
            }

            resolution = externalResolution;
            return true;
        }

        if (externalAliasResolution == ExternalTargetAliasResolution.Invalid)
        {
            resolution = default;
            validationMessage = externalValidationMessage;
            return false;
        }

        string fullPath;
        try
        {
            fullPath = ResolveWorkspaceFullPath(path);
        }
        catch (WorkspacePathResolutionException exception)
        {
            resolution = default;
            validationMessage = exception.SafeMessage;
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            resolution = default;
            validationMessage = "The requested workspace path is invalid.";
            return false;
        }

        if (!IsWithinWorkspace(fullPath))
        {
            resolution = default;
            validationMessage = $"Path '{ToSafeExternalPathReference(fullPath)}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.";
            return false;
        }

        if (!TryValidateNoReparseTraversal(fullPath, out validationMessage))
        {
            resolution = default;
            return false;
        }

        resolution = CreateWorkspaceResolution(fullPath);
        return true;
    }

    public WorkspacePathResolution ResolveAccessiblePath(string path, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        var externalAliasResolution = TryResolveExternalTargetAlias(path, out var externalResolution, out var externalValidationMessage);
        if (externalAliasResolution == ExternalTargetAliasResolution.Resolved)
        {
            if (allowedExternalRoots is not null &&
                !NormalizeAllowedExternalRoots(allowedExternalRoots)
                    .Any(root => IsPathWithinRoot(externalResolution.FullPath, root)))
            {
                throw WorkspacePathResolutionException.OutsideWorkspace(
                    "The external-target alias is not covered by the explicit external-root allowlist.");
            }

            EnsureNoReparseTraversal(externalResolution.FullPath);
            return externalResolution;
        }

        if (externalAliasResolution == ExternalTargetAliasResolution.Invalid)
        {
            throw WorkspacePathResolutionException.InvalidPath(externalValidationMessage);
        }

        string fullPath;
        try
        {
            fullPath = ResolveWorkspaceFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw WorkspacePathResolutionException.InvalidPath(
                "The requested path could not be normalized.",
                exception);
        }
        if (IsWithinWorkspace(fullPath))
        {
            EnsureNoReparseTraversal(fullPath);
            return CreateWorkspaceResolution(fullPath);
        }

        var normalizedAllowedRoots = NormalizeAllowedExternalRoots(allowedExternalRoots);
        if (normalizedAllowedRoots.Any(root => IsPathWithinRoot(fullPath, root)))
        {
            EnsureNoReparseTraversal(fullPath);
            if (!TryBuildExternalTargetAliasFromFullPath(fullPath, out var externalAlias))
            {
                throw WorkspacePathResolutionException.InvalidPath(
                    "The external path could not be bound to an opaque external-target alias in this workspace scope.");
            }

            return new WorkspacePathResolution(
                FullPath: fullPath,
                RelativePath: externalAlias,
                DisplayPath: externalAlias,
                IsWorkspacePath: false);
        }

        throw WorkspacePathResolutionException.OutsideWorkspace(
            $"Path '{ToSafeExternalPathReference(fullPath)}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    public bool TryResolveAccessiblePath(
        string path,
        IReadOnlyList<string> allowedExternalRoots,
        out WorkspacePathResolution resolution,
        out string validationMessage)
    {
        try
        {
            resolution = ResolveAccessiblePath(path, allowedExternalRoots);
            validationMessage = string.Empty;
            return true;
        }
        catch (WorkspacePathResolutionException exception)
        {
            resolution = default;
            validationMessage = exception.SafeMessage;
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            resolution = default;
            validationMessage = "The requested path could not be resolved within the allowed external roots.";
            return false;
        }
    }

    public WorkspacePathResolution ResolveExistingPath(string path, bool allowFiles, bool allowDirectories, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        var resolution = ResolveAccessiblePath(path, allowedExternalRoots);
        var displayPath = resolution.DisplayPath;

        if (File.Exists(resolution.FullPath))
        {
            if (!allowFiles)
            {
                throw WorkspacePathResolutionException.DirectoryRequired(
                    $"Path '{displayPath}' resolves to a file, but a directory was required.");
            }

            return resolution;
        }

        if (Directory.Exists(resolution.FullPath))
        {
            if (!allowDirectories)
            {
                throw WorkspacePathResolutionException.FileRequired(
                    $"Path '{displayPath}' resolves to a directory, but a file was required.");
            }

            return resolution;
        }

        if (TryCreateManagedPathAliasCorrectionMessage(displayPath, out var aliasCorrectionMessage))
        {
            throw WorkspacePathResolutionException.ManagedPathAliasMismatch(aliasCorrectionMessage);
        }

        throw WorkspacePathResolutionException.PathMissing(
            $"Path '{displayPath}' does not exist.");
    }

    public string ResolveWorkingDirectory(
        string? workingDirectory,
        bool createIfMissing,
        out WorkspacePathResolution resolution,
        IReadOnlyList<string>? allowedExternalRoots = null)
    {
        resolution = string.IsNullOrWhiteSpace(workingDirectory)
            ? CreateWorkspaceResolution(workspaceRoot)
            : ResolveAccessiblePath(workingDirectory, allowedExternalRoots);
        EnsureNoReparseTraversal(resolution.FullPath);

        if (File.Exists(resolution.FullPath))
        {
            throw WorkspacePathResolutionException.DirectoryRequired(
                $"Working directory '{resolution.DisplayPath}' resolves to a file.");
        }

        if (!Directory.Exists(resolution.FullPath))
        {
            if (!createIfMissing)
            {
                throw WorkspacePathResolutionException.PathMissing(
                    $"Working directory '{resolution.DisplayPath}' does not exist.");
            }

            EnsureDirectoryForMutation(resolution.FullPath);
        }

        return resolution.DisplayPath;
    }

    public bool IsWithinWorkspace(string fullPath)
        => workspacePathPolicy.IsWithinRoot(fullPath);

    public string ToRelativePath(string fullPath)
    {
        if (workspacePathPolicy.PathComparer.Equals(fullPath, workspaceRoot))
        {
            return ".";
        }

        if (IsWithinWorkspace(fullPath))
        {
            return NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, fullPath));
        }

        if (TryBuildExternalTargetAliasFromFullPath(fullPath, out var externalAlias))
        {
            return externalAlias;
        }

        throw WorkspacePathResolutionException.InvalidPath(
            "The external path could not be bound to an opaque external-target alias in this workspace scope.");
    }

    public string ToDisplayPath(string fullPath)
        => ToRelativePath(fullPath);

    public IReadOnlyList<string> NormalizeAllowedExternalRoots(IReadOnlyList<string>? allowedExternalRoots)
    {
        return allowedExternalRoots?
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => ResolveWorkspaceFullPath(root!))
            .ToList()
            ?? [];
    }

    public static string NormalizeRelativePath(string path)
    {
        var trimmedPath = path.Trim();
        return string.Equals(trimmedPath, ".", StringComparison.Ordinal)
            ? trimmedPath
            : LogicalPath.ParseLegacyWindowsLogicalPath(trimmedPath).Value;
    }

    public static bool TryCreateManagedPathAliasCorrectionMessage(string? path, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = NormalizeRelativePath(path);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var correctedSegments = segments.Select(NormalizeManagedPathAliasSegment).ToArray();
        var hasManagedAliasCorrection = segments
            .Zip(correctedSegments)
            .Any(pair => !string.Equals(pair.First, pair.Second, StringComparison.Ordinal));
        if (!hasManagedAliasCorrection)
        {
            return false;
        }

        var corrected = string.Join('/', correctedSegments);
        message = $"Path '{normalized}' uses underscore managed-file segment(s). Use exact workspace path '{corrected}'. Managed project-media paths use hyphenated segments.";
        return true;
    }

    private static string NormalizeManagedPathAliasSegment(string segment)
    {
        return segment switch
        {
            _ when string.Equals(segment, "managed_files", StringComparison.OrdinalIgnoreCase) => "managed-files",
            _ when string.Equals(segment, "project_media", StringComparison.OrdinalIgnoreCase) => "project-media",
            _ => segment
        };
    }

    public bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(fullPath);
        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(rootPath);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedRoot = Path.GetFullPath(rootPath);
        try
        {
            return physicalPathPolicyFactory.Create(normalizedRoot).IsWithinRoot(normalizedFullPath);
        }
        catch (PhysicalPathValidationException)
        {
            return false;
        }
    }

    public static string ExpandPortablePath(string path)
    {
        return ExpandPortablePath(
            path,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);
    }

    internal static string ExpandPortablePath(
        string path,
        string? homeDirectory,
        Func<string, string?> variableResolver,
        PortablePathTemplateCompatibility compatibility)
    {
        return PortablePathTemplate.Expand(path, homeDirectory, variableResolver, compatibility);
    }

    private string ResolveWorkspaceFullPath(string path)
    {
        var externalAliasResolution = TryResolveExternalTargetAlias(path, out var externalResolution, out var externalValidationMessage);
        if (externalAliasResolution == ExternalTargetAliasResolution.Resolved)
        {
            return externalResolution.FullPath;
        }

        if (externalAliasResolution == ExternalTargetAliasResolution.Invalid)
        {
            throw WorkspacePathResolutionException.InvalidPath(externalValidationMessage);
        }

        var trimmedPath = path.Trim();
        var expandedPath = IsExternalTargetAliasPrefix(trimmedPath)
            ? trimmedPath
            : ExpandPortablePath(path);
        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(expandedPath);
        var nativePath = Path.IsPathRooted(expandedPath)
            ? expandedPath
            : ToNativeLogicalPath(expandedPath);
        var candidateFullPath = Path.GetFullPath(
            Path.IsPathRooted(nativePath)
                ? nativePath
                : Path.Combine(workspaceRoot, nativePath));
        if (!IsWithinWorkspace(candidateFullPath))
        {
            return candidateFullPath;
        }

        var relativePath = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, candidateFullPath));
        if (string.IsNullOrWhiteSpace(relativePath) || string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return candidateFullPath;
        }

        var scopedRelativePath = ApplyManagedRootScope(relativePath);
        return Path.GetFullPath(Path.Combine(workspaceRoot, scopedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ResolveFullPath(string path, string workspaceRoot)
    {
        var expandedPath = ExpandPortablePath(path);
        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(expandedPath);
        var nativePath = Path.IsPathRooted(expandedPath)
            ? expandedPath
            : ToNativeLogicalPath(expandedPath);
        return Path.GetFullPath(Path.IsPathRooted(nativePath) ? nativePath : Path.Combine(workspaceRoot, nativePath));
    }

    private WorkspacePathResolution CreateWorkspaceResolution(string fullPath)
    {
        var relativePath = ToRelativePath(fullPath);
        return new WorkspacePathResolution(
            FullPath: fullPath,
            RelativePath: relativePath,
            DisplayPath: relativePath,
            IsWorkspacePath: true);
    }

    public static bool IsExternalTargetAliasPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmedPath = path.Trim();
        if (!trimmedPath.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase) &&
            !trimmedPath.StartsWith(ExternalTargetAliasRoot + "\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return MatchesRoot(NormalizeRelativePath(trimmedPath), ExternalTargetAliasRoot);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal bool TryValidateNoReparseTraversal(string fullPath, out string validationMessage)
    {
        try
        {
            EnsureNoReparseTraversal(fullPath);
            validationMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            validationMessage = exception is WorkspacePathResolutionException pathFailure
                ? pathFailure.SafeMessage
                : "The requested path could not be validated against filesystem reparse-point traversal.";
            return false;
        }
    }

    private void EnsureNoReparseTraversal(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        IPhysicalFileSystemPathPolicy policy;
        if (workspacePathPolicy.IsWithinRoot(normalizedFullPath))
        {
            policy = workspacePathPolicy;
        }
        else
        {
            var rootPath = Path.GetPathRoot(normalizedFullPath);
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw WorkspacePathResolutionException.InvalidPath(
                    "The requested path does not have a filesystem root.");
            }

            policy = physicalPathPolicyFactory.Create(rootPath);
        }

        try
        {
            policy.EnsureSafePath(normalizedFullPath, allowMissingLeaf: true);
        }
        catch (PhysicalPathValidationException exception)
        {
            throw WorkspacePathResolutionException.ReparsePointTraversal(
                exception.ErrorCode == PhysicalPathValidationErrorCode.LinkTraversal
                    ? "Filesystem symbolic-link or reparse-point traversal is not allowed for workspace paths."
                    : "The requested workspace path could not be validated safely.");
        }
    }

    private void RevalidateMutationTarget(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        IPhysicalFileSystemPathPolicy policy;
        if (workspacePathPolicy.IsWithinRoot(normalizedFullPath))
        {
            policy = workspacePathPolicy;
        }
        else
        {
            string rootPath = Path.GetPathRoot(normalizedFullPath)
                ?? throw WorkspacePathResolutionException.InvalidPath(
                    "The requested mutation target does not have a filesystem root.");
            policy = physicalPathPolicyFactory.Create(rootPath);
        }

        try
        {
            if (policy.PathComparer.Equals(normalizedFullPath, policy.RootPath))
            {
                policy.EnsureSafePath(normalizedFullPath);
                return;
            }

            policy.RevalidateMutationTarget(normalizedFullPath);
        }
        catch (PhysicalPathValidationException exception)
        {
            throw WorkspacePathResolutionException.ReparsePointTraversal(
                exception.ErrorCode == PhysicalPathValidationErrorCode.LinkTraversal
                    ? "Filesystem symbolic-link or reparse-point traversal is not allowed for workspace paths."
                    : "The requested workspace mutation target could not be validated safely.");
        }
    }

    internal void ValidateMutationTarget(string fullPath)
        => RevalidateMutationTarget(fullPath);

    internal void ValidatePathForUse(string fullPath)
        => EnsureNoReparseTraversal(fullPath);

    internal void EnsureParentDirectoryForMutation(string fullPath)
    {
        string parentPath = Path.GetDirectoryName(Path.GetFullPath(fullPath))
            ?? throw WorkspacePathResolutionException.InvalidPath(
                "The requested mutation target does not have a parent directory.");
        EnsureDirectoryForMutation(parentPath);
    }

    internal void EnsureDirectoryForMutation(string fullPath)
    {
        string normalizedPath = Path.GetFullPath(fullPath);
        bool isWorkspacePath = workspacePathPolicy.IsWithinRoot(normalizedPath);
        if (!isWorkspacePath)
        {
            if (Directory.Exists(normalizedPath))
            {
                RevalidateMutationTarget(normalizedPath);
                return;
            }

            string externalParentPath = Path.GetDirectoryName(normalizedPath)
                ?? throw WorkspacePathResolutionException.InvalidPath(
                    "The requested external directory mutation does not have a parent directory.");
            if (!Directory.Exists(externalParentPath))
            {
                throw WorkspacePathResolutionException.InvalidPath(
                    "The parent of an authorized external directory mutation must already exist.");
            }

            RevalidateMutationTarget(normalizedPath);
            Directory.CreateDirectory(normalizedPath);
            RevalidateMutationTarget(normalizedPath);
            return;
        }

        if (Directory.Exists(normalizedPath))
        {
            RevalidateMutationTarget(normalizedPath);
            return;
        }

        if (!workspacePathPolicy.PathComparer.Equals(normalizedPath, workspaceRoot))
        {
            string parentPath = Path.GetDirectoryName(normalizedPath)
                ?? throw WorkspacePathResolutionException.InvalidPath(
                    "The requested directory mutation does not have a parent directory.");
            EnsureDirectoryForMutation(parentPath);
        }

        RevalidateMutationTarget(normalizedPath);
        Directory.CreateDirectory(normalizedPath);
        RevalidateMutationTarget(normalizedPath);
    }

    internal bool AreEquivalentPhysicalPaths(string leftPath, string rightPath)
        => workspacePathPolicy.PathComparer.Equals(
            Path.GetFullPath(leftPath),
            Path.GetFullPath(rightPath));

    internal StringComparer GetPhysicalPathComparer(string rootPath)
    {
        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(rootPath);
        return physicalPathPolicyFactory.Create(Path.GetFullPath(rootPath)).PathComparer;
    }

    internal IPhysicalFileSystemPathPolicy GetPhysicalPathPolicy(string rootPath)
        => physicalPathPolicyFactory.Create(Path.GetFullPath(rootPath));

    private bool TryBuildExternalTargetAliasFromFullPath(string fullPath, out string aliasPath)
    {
        aliasPath = string.Empty;
        return externalTargetRegistry?.TryCreateAlias(fullPath, out aliasPath) == true;
    }

    private string ToSafeExternalPathReference(string fullPath)
        => TryBuildExternalTargetAliasFromFullPath(fullPath, out var aliasPath)
            ? aliasPath
            : "external-target/unresolved";

    private ExternalTargetAliasResolution TryResolveExternalTargetAlias(
        string? path,
        out WorkspacePathResolution resolution,
        out string validationMessage)
    {
        resolution = default;
        validationMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return ExternalTargetAliasResolution.NotMatched;
        }

        var trimmedPath = path.Trim();
        var expandedPath = IsExternalTargetAliasPrefix(trimmedPath)
            ? trimmedPath
            : ExpandPortablePath(path);
        try
        {
            WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(expandedPath);
        }
        catch (WorkspacePathResolutionException exception)
        {
            validationMessage = exception.SafeMessage;
            return ExternalTargetAliasResolution.Invalid;
        }

        if (Path.IsPathRooted(expandedPath))
        {
            return ExternalTargetAliasResolution.NotMatched;
        }

        var trimmedExpandedPath = expandedPath.Trim();
        if (!IsExternalTargetAliasPrefix(trimmedExpandedPath))
        {
            return ExternalTargetAliasResolution.NotMatched;
        }

        string normalizedPath;
        try
        {
            normalizedPath = NormalizeRelativePath(expandedPath);
        }
        catch (ArgumentException)
        {
            var invalidSegments = expandedPath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            validationMessage = invalidSegments.Any(IsDotPathSegment)
                ? $"Path '{path}' uses invalid external-target traversal segments. Use a canonical alias without '.' or '..' segments."
                : $"Path '{path}' uses invalid external-target logical syntax.";
            return ExternalTargetAliasResolution.Invalid;
        }

        if (!MatchesRoot(normalizedPath, ExternalTargetAliasRoot))
        {
            return ExternalTargetAliasResolution.NotMatched;
        }

        var versionedResolution = externalTargetRegistry?.TryResolve(
                normalizedPath,
                out var versionedFullPath,
                out var versionedValidationMessage)
            ?? ResolveUnboundVersionedAlias(
                normalizedPath,
                out versionedFullPath,
                out versionedValidationMessage);
        if (versionedResolution == ExternalTargetAliasResolutionKind.Resolved)
        {
            var canonicalAlias = ExternalTargetAliasCodec.NormalizeVersionedAlias(normalizedPath)!;
            resolution = new WorkspacePathResolution(
                FullPath: versionedFullPath,
                RelativePath: canonicalAlias,
                DisplayPath: canonicalAlias,
                IsWorkspacePath: false);
            return ExternalTargetAliasResolution.Resolved;
        }

        if (versionedResolution is ExternalTargetAliasResolutionKind.Invalid or ExternalTargetAliasResolutionKind.Unbound)
        {
            validationMessage = versionedValidationMessage;
            return ExternalTargetAliasResolution.Invalid;
        }

        var suffix = RemoveRoot(normalizedPath, ExternalTargetAliasRoot);
        if (string.IsNullOrWhiteSpace(suffix))
        {
            validationMessage = $"Path '{path}' targets the mapped external-target root. Use a path like '{ExternalTargetAliasRoot}/C/path/to/project'.";
            return ExternalTargetAliasResolution.Invalid;
        }

        var segments = suffix
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(IsDotPathSegment))
        {
            validationMessage =
                $"Path '{path}' uses invalid external-target traversal segments. Use a canonical alias without '.' or '..' segments.";
            return ExternalTargetAliasResolution.Invalid;
        }

        if (segments.Length == 0 ||
            segments[0].Length != 1 ||
            !char.IsLetter(segments[0][0]))
        {
            validationMessage = $"Path '{path}' uses invalid external-target syntax. Use '{ExternalTargetAliasRoot}/<drive-letter>/path/to/target'.";
            return ExternalTargetAliasResolution.Invalid;
        }

        if (!OperatingSystem.IsWindows())
        {
            validationMessage =
                $"Path '{path}' uses a legacy Windows drive alias that is host-bound and cannot be resolved on this host. Rebind it to a versioned external-target root.";
            return ExternalTargetAliasResolution.Invalid;
        }

        var driveLetter = char.ToUpperInvariant(segments[0][0]);
        if (segments.Length == 1)
        {
            validationMessage =
                $"Path '{path}' targets an external drive root. Use a specific grounded path like '{ExternalTargetAliasRoot}/{driveLetter}/path/to/project'.";
            return ExternalTargetAliasResolution.Invalid;
        }

        var rootPath = $"{driveLetter}:{Path.DirectorySeparatorChar}";
        var remainingSegments = segments.Skip(1).ToArray();
        var mappedFullPath = Path.Combine(rootPath, Path.Combine(remainingSegments));
        var normalizedFullPath = Path.GetFullPath(mappedFullPath);
        var aliasPath = NormalizeRelativePath(
            Path.Combine(
                ExternalTargetAliasRoot,
                driveLetter.ToString(),
                Path.Combine(remainingSegments)));

        resolution = new WorkspacePathResolution(
            FullPath: normalizedFullPath,
            RelativePath: aliasPath,
            DisplayPath: aliasPath,
            IsWorkspacePath: false);
        return ExternalTargetAliasResolution.Resolved;
    }

    private static ExternalTargetAliasResolutionKind ResolveUnboundVersionedAlias(
        string alias,
        out string fullPath,
        out string validationMessage)
    {
        fullPath = string.Empty;
        if (!ExternalTargetAliasCodec.IsVersionedAlias(alias))
        {
            validationMessage = string.Empty;
            return ExternalTargetAliasResolutionKind.NotVersionedAlias;
        }

        validationMessage =
            "The external-target root is not bound in this workspace scope and requires explicit rebind or migration.";
        return ExternalTargetAliasResolutionKind.Unbound;
    }

    private static bool IsDotPathSegment(string segment)
        => string.Equals(segment, ".", StringComparison.Ordinal) ||
           string.Equals(segment, "..", StringComparison.Ordinal);

    private static string ToNativeLogicalPath(string path)
    {
        if (string.Equals(path, ".", StringComparison.Ordinal))
        {
            return path;
        }

        var logicalPath = LogicalPath.ParseLegacyWindowsLogicalPath(path);
        return Path.Combine(logicalPath.Segments.ToArray());
    }

    private string ApplyManagedRootScope(string relativePath)
    {
        if (workspaceScope.IsDefaultSandbox)
        {
            return relativePath;
        }

        return TryMapManagedRoot(relativePath, "artifacts", workspaceScope.ArtifactRootRelativePath)
            ?? TryMapManagedRoot(relativePath, "output", workspaceScope.OutputRootRelativePath)
            ?? TryMapManagedRoot(relativePath, "integration-map", workspaceScope.IntegrationMapRootRelativePath)
            ?? TryMapManagedRoot(relativePath, "data", workspaceScope.DataRootRelativePath)
            ?? relativePath;
    }

    private string? TryMapManagedRoot(string relativePath, string rootName, string scopedRootRelativePath)
    {
        if (!MatchesRoot(relativePath, rootName))
        {
            return null;
        }

        if (MatchesRoot(relativePath, scopedRootRelativePath))
        {
            return relativePath;
        }

        var foreignScopedPrefix = $"{rootName}/scopes/";
        if (relativePath.StartsWith(foreignScopedPrefix, StringComparison.Ordinal))
        {
            throw WorkspacePathResolutionException.ForeignManagedScope(
                $"Path '{relativePath}' targets a different managed {rootName} scope. Use the current scope '{workspaceScope.DisplayName}'.");
        }

        var suffix = RemoveRoot(relativePath, rootName);
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool MatchesRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.Ordinal)
               || relativePath.StartsWith(rootRelativePath + "/", StringComparison.Ordinal);
    }

    private static string RemoveRoot(string relativePath, string rootRelativePath)
    {
        if (string.Equals(relativePath, rootRelativePath, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return relativePath[(rootRelativePath.Length + 1)..];
    }

    private static bool IsExternalTargetAliasPrefix(string path)
    {
        return path.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(ExternalTargetAliasRoot + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private enum ExternalTargetAliasResolution
    {
        NotMatched,
        Resolved,
        Invalid
    }
}
