using CanDoItAll.AgentFramework.Models;

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
    private readonly string workspaceRootWithSeparator;
    private readonly WorkspaceScopeDescriptor workspaceScope;

    public WorkspacePathPolicy(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null)
    {
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        workspaceRootWithSeparator = EnsureTrailingSeparator(this.workspaceRoot);
        this.workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
    }

    public string WorkspaceRoot => workspaceRoot;

    public WorkspaceScopeDescriptor WorkspaceScope => workspaceScope;

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
        catch (InvalidOperationException exception)
        {
            resolution = default;
            validationMessage = exception.Message;
            return false;
        }

        if (!IsWithinWorkspace(fullPath))
        {
            resolution = default;
            validationMessage = $"Path '{path}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.";
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
            EnsureNoReparseTraversal(externalResolution.FullPath);
            return externalResolution;
        }

        if (externalAliasResolution == ExternalTargetAliasResolution.Invalid)
        {
            throw new InvalidOperationException(externalValidationMessage);
        }

        var fullPath = ResolveWorkspaceFullPath(path);
        if (IsWithinWorkspace(fullPath))
        {
            EnsureNoReparseTraversal(fullPath);
            return CreateWorkspaceResolution(fullPath);
        }

        var normalizedAllowedRoots = NormalizeAllowedExternalRoots(allowedExternalRoots);
        if (normalizedAllowedRoots.Any(root => IsPathWithinRoot(fullPath, root)))
        {
            EnsureNoReparseTraversal(fullPath);
            var normalizedAbsolutePath = NormalizeAbsolutePath(fullPath);
            return new WorkspacePathResolution(
                FullPath: fullPath,
                RelativePath: normalizedAbsolutePath,
                DisplayPath: normalizedAbsolutePath,
                IsWorkspacePath: false);
        }

        throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    public WorkspacePathResolution ResolveExistingPath(string path, bool allowFiles, bool allowDirectories, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        var resolution = ResolveAccessiblePath(path, allowedExternalRoots);
        var displayPath = resolution.DisplayPath;

        if (File.Exists(resolution.FullPath))
        {
            if (!allowFiles)
            {
                throw new InvalidOperationException($"Path '{displayPath}' resolves to a file, but a directory was required.");
            }

            return resolution;
        }

        if (Directory.Exists(resolution.FullPath))
        {
            if (!allowDirectories)
            {
                throw new InvalidOperationException($"Path '{displayPath}' resolves to a directory, but a file was required.");
            }

            return resolution;
        }

        if (TryCreateManagedPathAliasCorrectionMessage(displayPath, out var aliasCorrectionMessage))
        {
            throw new InvalidOperationException(aliasCorrectionMessage);
        }

        throw new InvalidOperationException($"Path '{displayPath}' does not exist.");
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
            throw new InvalidOperationException($"Working directory '{resolution.DisplayPath}' resolves to a file.");
        }

        if (!Directory.Exists(resolution.FullPath))
        {
            if (!createIfMissing)
            {
                throw new InvalidOperationException($"Working directory '{resolution.DisplayPath}' does not exist.");
            }

            Directory.CreateDirectory(resolution.FullPath);
            EnsureNoReparseTraversal(resolution.FullPath);
        }

        return resolution.DisplayPath;
    }

    public bool IsWithinWorkspace(string fullPath)
    {
        return string.Equals(fullPath, workspaceRoot, FileSystemPathComparison)
            || fullPath.StartsWith(workspaceRootWithSeparator, FileSystemPathComparison);
    }

    public string ToRelativePath(string fullPath)
    {
        if (string.Equals(fullPath, workspaceRoot, FileSystemPathComparison))
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

        return NormalizeAbsolutePath(fullPath);
    }

    public string ToDisplayPath(string fullPath)
        => ToRelativePath(fullPath);

    public IReadOnlyList<string> NormalizeAllowedExternalRoots(IReadOnlyList<string>? allowedExternalRoots)
    {
        return allowedExternalRoots?
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => ResolveWorkspaceFullPath(root!))
            .Distinct(FileSystemPathComparer)
            .ToList()
            ?? [];
    }

    public static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').Trim();

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

    public static string NormalizeAbsolutePath(string path)
        => Path.GetFullPath(path).Replace('\\', '/');

    public static bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedRoot = Path.GetFullPath(rootPath);
        if (string.Equals(normalizedFullPath, normalizedRoot, FileSystemPathComparison))
        {
            return true;
        }

        var normalizedRootWithSeparator = EnsureTrailingSeparator(normalizedRoot);
        return normalizedFullPath.StartsWith(normalizedRootWithSeparator, FileSystemPathComparison);
    }

    public static string ExpandPortablePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (expanded.StartsWith("~/", StringComparison.Ordinal) || expanded.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, expanded[2..]);
        }

        return expanded;
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
            throw new InvalidOperationException(externalValidationMessage);
        }

        var expandedPath = ExpandPortablePath(path);
        var candidateFullPath = Path.GetFullPath(
            Path.IsPathRooted(expandedPath)
                ? expandedPath
                : Path.Combine(workspaceRoot, expandedPath));
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
        return Path.GetFullPath(Path.IsPathRooted(expandedPath) ? expandedPath : Path.Combine(workspaceRoot, expandedPath));
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

        var normalizedPath = NormalizeRelativePath(ExpandPortablePath(path));
        return MatchesRoot(normalizedPath, ExternalTargetAliasRoot);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static StringComparison FileSystemPathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static StringComparer FileSystemPathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static bool TryValidateNoReparseTraversal(string fullPath, out string validationMessage)
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
            validationMessage = exception is InvalidOperationException
                ? exception.Message
                : "The requested path could not be validated against filesystem reparse-point traversal.";
            return false;
        }
    }

    private static void EnsureNoReparseTraversal(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var rootPath = Path.GetPathRoot(normalizedFullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("The requested path does not have a filesystem root.");
        }

        var relativePath = Path.GetRelativePath(rootPath, normalizedFullPath);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return;
        }

        var currentPath = rootPath;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(currentPath);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                break;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException(
                    "Filesystem reparse-point traversal is not allowed for workspace paths.");
            }
        }
    }

    private static bool TryBuildExternalTargetAliasFromFullPath(string fullPath, out string aliasPath)
    {
        aliasPath = string.Empty;
        if (string.IsNullOrWhiteSpace(fullPath) || !Path.IsPathRooted(fullPath))
        {
            return false;
        }

        var normalizedFullPath = Path.GetFullPath(fullPath);
        var rootPath = Path.GetPathRoot(normalizedFullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var trimmedRootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRootPath.Length != 2 ||
            trimmedRootPath[1] != ':' ||
            !char.IsLetter(trimmedRootPath[0]))
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(trimmedRootPath[0]);
        var relativeWithinDrive = normalizedFullPath.Length <= rootPath.Length
            ? string.Empty
            : normalizedFullPath[rootPath.Length..]
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

        aliasPath = string.IsNullOrWhiteSpace(relativeWithinDrive)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : NormalizeRelativePath(Path.Combine(ExternalTargetAliasRoot, driveLetter.ToString(), relativeWithinDrive));
        return true;
    }

    private static ExternalTargetAliasResolution TryResolveExternalTargetAlias(
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

        var expandedPath = ExpandPortablePath(path);
        if (Path.IsPathRooted(expandedPath))
        {
            return ExternalTargetAliasResolution.NotMatched;
        }

        var normalizedPath = NormalizeRelativePath(expandedPath);
        if (!MatchesRoot(normalizedPath, ExternalTargetAliasRoot))
        {
            return ExternalTargetAliasResolution.NotMatched;
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

    private static bool IsDotPathSegment(string segment)
        => string.Equals(segment, ".", StringComparison.Ordinal) ||
           string.Equals(segment, "..", StringComparison.Ordinal);

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
        if (relativePath.StartsWith(foreignScopedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Path '{relativePath}' targets a different managed {rootName} scope. Use the current scope '{workspaceScope.DisplayName}'.");
        }

        var suffix = RemoveRoot(relativePath, rootName);
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool MatchesRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveRoot(string relativePath, string rootRelativePath)
    {
        if (string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return relativePath[(rootRelativePath.Length + 1)..];
    }

    private enum ExternalTargetAliasResolution
    {
        NotMatched,
        Resolved,
        Invalid
    }
}
