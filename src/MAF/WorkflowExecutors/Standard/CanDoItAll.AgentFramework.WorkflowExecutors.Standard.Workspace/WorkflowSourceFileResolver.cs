using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

internal sealed class WorkflowSourceFileResolver
{
    private static readonly char[] PathTrimCharacters = [' ', '\t', '\r', '\n', '`', '\'', '"'];
    private readonly IWorkspacePathResolutionService paths;
    private readonly IExternalTargetPathRegistry externalTargetPathRegistry;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;
    private readonly Func<string, bool, IEnumerable<string>> fileEnumerator;

    public WorkflowSourceFileResolver(
        IWorkspacePathResolutionService paths,
        IExternalTargetPathRegistry externalTargetPathRegistry,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        Func<string, bool, IEnumerable<string>>? fileEnumerator = null)
    {
        this.paths = paths;
        this.externalTargetPathRegistry = externalTargetPathRegistry;
        this.physicalPathPolicyFactory = physicalPathPolicyFactory;
        this.fileEnumerator = fileEnumerator ?? EnumerateFiles;
    }

    public IEnumerable<WorkflowSourceIngestionFile> ResolveCandidateFiles(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionExecutorSettings settings,
        IReadOnlySet<string> allowedExtensions,
        int take)
    {
        if (take <= 0)
        {
            yield break;
        }

        var resolvedAsDirectory = string.Equals(candidate.Kind, "folderPath", StringComparison.OrdinalIgnoreCase) ||
                                  (!string.Equals(candidate.Kind, "filePath", StringComparison.OrdinalIgnoreCase) &&
                                   Directory.Exists(ResolvePathForProbe(candidate.Value, settings)));
        if (resolvedAsDirectory)
        {
            var directory = ResolveDirectory(candidate.Value, settings);
            IPhysicalFileSystemPathPolicy directoryPathPolicy = physicalPathPolicyFactory.Create(directory.FullPath);
            var count = 0;
            foreach (var file in EnumerateAccessibleFiles(
                         directory,
                         directoryPathPolicy,
                         settings.RecursiveFolders)
                     .OrderBy(
                         path => NormalizeEnumerationKey(Path.GetRelativePath(directory.FullPath, path)),
                         StringComparer.Ordinal)
                     .ThenBy(path => path, StringComparer.Ordinal))
            {
                if (!IsAllowedExtension(file, allowedExtensions))
                {
                    continue;
                }

                yield return new WorkflowSourceIngestionFile(
                    file,
                    ToDisplayPath(file, directory, directoryPathPolicy),
                    Path.GetFileName(file));
                count++;
                if (count >= take)
                {
                    yield break;
                }
            }

            yield break;
        }

        var resolvedFile = ResolveFile(candidate.Value, settings);
        if (!IsAllowedExtension(resolvedFile.FullPath, allowedExtensions))
        {
            throw new InvalidOperationException(
                $"Source file '{resolvedFile.RelativePath}' has extension '{Path.GetExtension(resolvedFile.FullPath)}', which is not allowed by this workflow source-ingestion node.");
        }

        yield return new WorkflowSourceIngestionFile(
            resolvedFile.FullPath,
            resolvedFile.RelativePath,
            Path.GetFileName(resolvedFile.FullPath));
    }

    private WorkspaceResolvedPath ResolveFile(
        string value,
        WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveFilePath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && IsNativeAbsolutePath(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source file '{fullPath}' was not found.");
            }

            physicalPathPolicyFactory.Create(fullPath).EnsureSafePath(fullPath);

            return new WorkspaceResolvedPath(fullPath, ToExternalTargetAlias(fullPath), IsWorkspacePath: false);
        }
    }

    private IEnumerable<string> EnumerateAccessibleFiles(
        WorkspaceResolvedPath directory,
        IPhysicalFileSystemPathPolicy directoryPathPolicy,
        bool recursive)
    {
        IEnumerator<string> enumerator;
        try
        {
            enumerator = fileEnumerator(directory.FullPath, recursive).GetEnumerator();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePath(directory.RelativePath);
        }

        using (enumerator)
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = enumerator.MoveNext();
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
                {
                    throw WorkspaceToolAccessDeniedException.InaccessiblePath(directory.RelativePath);
                }

                if (!hasNext)
                {
                    yield break;
                }

                try
                {
                    directoryPathPolicy.EnsureSafePath(enumerator.Current);
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
                {
                    throw WorkspaceToolAccessDeniedException.InaccessiblePath(directory.RelativePath);
                }

                yield return enumerator.Current;
            }
        }
    }

    private static IEnumerable<string> EnumerateFiles(string directory, bool recursive)
        => Directory.EnumerateFiles(
            directory,
            "*",
            new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            });

    private WorkspaceResolvedPath ResolveDirectory(
        string value,
        WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && IsNativeAbsolutePath(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source directory '{fullPath}' was not found.");
            }

            physicalPathPolicyFactory.Create(fullPath).EnsureSafePath(fullPath);

            return new WorkspaceResolvedPath(fullPath, ToExternalTargetAlias(fullPath), IsWorkspacePath: false);
        }
    }

    private string ResolvePathForProbe(
        string value,
        WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        if (PhysicalPathSyntaxClassifier.Classify(path) != PhysicalPathSyntax.Relative)
        {
            if (!settings.AllowAbsoluteInputPaths || !IsNativeAbsolutePath(path))
            {
                return path;
            }

            return Path.GetFullPath(path);
        }

        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false).FullPath;
        }
        catch (InvalidOperationException)
        {
            return path;
        }
    }

    private static bool IsAllowedExtension(
        string fullPath,
        IReadOnlySet<string> allowedExtensions)
        => allowedExtensions.Count == 0 || allowedExtensions.Contains(Path.GetExtension(fullPath));

    private static string NormalizeInputPath(string value)
        => value.Trim(PathTrimCharacters);

    public string ToSafeDisplayPath(string value)
    {
        var path = NormalizeInputPath(value);
        var syntax = PhysicalPathSyntaxClassifier.Classify(path);
        var nativeAbsolute = IsNativeAbsolutePath(path);
        if (!nativeAbsolute)
        {
            return syntax == PhysicalPathSyntax.Relative
                ? value
                : "external-target/unresolved";
        }

        try
        {
            return ToExternalTargetAlias(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            return "external-target/unresolved";
        }
    }

    private string ToExternalTargetAlias(string fullPath)
    {
        if (externalTargetPathRegistry.TryCreateAlias(fullPath, out var alias))
        {
            return alias;
        }

        throw new InvalidOperationException(
            "The external source path could not be bound to an opaque external-target alias on this host.");
    }

    private string ToDisplayPath(
        string fullPath,
        WorkspaceResolvedPath directory,
        IPhysicalFileSystemPathPolicy directoryPathPolicy)
    {
        if (!directory.IsWorkspacePath)
        {
            return ToExternalTargetAlias(fullPath);
        }

        if (!directoryPathPolicy.IsWithinRoot(fullPath))
        {
            return ToExternalTargetAlias(fullPath);
        }

        var relativePath = NormalizeEnumerationKey(Path.GetRelativePath(directory.FullPath, fullPath));
        var logicalCandidate = $"{directory.RelativePath.TrimEnd('/')}/{relativePath}";
        return LogicalPath.TryParse(logicalCandidate, out var logicalPath)
            ? logicalPath!.Value
            : ToExternalTargetAlias(fullPath);
    }

    private static string NormalizeEnumerationKey(string relativePath)
        => relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static bool IsNativeAbsolutePath(string path)
    {
        var syntax = PhysicalPathSyntaxClassifier.Classify(path);
        return syntax == PhysicalPathSyntax.UnixAbsolute && !OperatingSystem.IsWindows() ||
               syntax is PhysicalPathSyntax.WindowsDriveAbsolute or PhysicalPathSyntax.WindowsUnc && OperatingSystem.IsWindows();
    }

}
