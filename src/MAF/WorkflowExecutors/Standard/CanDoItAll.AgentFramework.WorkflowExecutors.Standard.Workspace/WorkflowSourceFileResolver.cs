using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

internal sealed class WorkflowSourceFileResolver(IWorkspacePathResolutionService paths)
{
    private static readonly char[] PathTrimCharacters = [' ', '\t', '\r', '\n', '`', '\'', '"'];

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
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(
                         directory.FullPath,
                         "*",
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = settings.RecursiveFolders,
                             IgnoreInaccessible = true,
                             AttributesToSkip = 0
                         })
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsAllowedExtension(file, allowedExtensions))
                {
                    continue;
                }

                yield return new WorkflowSourceIngestionFile(
                    file,
                    ToDisplayPath(file, directory),
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
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source file '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private WorkspaceResolvedPath ResolveDirectory(
        string value,
        WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source directory '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private string ResolvePathForProbe(
        string value,
        WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        if (Path.IsPathRooted(path))
        {
            return settings.AllowAbsoluteInputPaths
                ? Path.GetFullPath(path)
                : path;
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
        => value.Trim(PathTrimCharacters).Replace('/', Path.DirectorySeparatorChar);

    private static string NormalizeAbsoluteDisplayPath(string value)
        => Path.GetFullPath(value).Replace('\\', '/');

    private static string ToDisplayPath(string fullPath, WorkspaceResolvedPath directory)
    {
        if (!directory.IsWorkspacePath)
        {
            return NormalizeAbsoluteDisplayPath(fullPath);
        }

        return NormalizeAbsoluteDisplayPath(fullPath).StartsWith(
            NormalizeAbsoluteDisplayPath(directory.FullPath),
            StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(directory.RelativePath, Path.GetRelativePath(directory.FullPath, fullPath)).Replace('\\', '/')
            : NormalizeAbsoluteDisplayPath(fullPath);
    }
}
