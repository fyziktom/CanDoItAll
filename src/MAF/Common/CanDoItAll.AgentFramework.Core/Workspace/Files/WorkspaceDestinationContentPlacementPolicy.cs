using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Core;

internal readonly record struct WorkspaceDestinationContentCandidate(
    string FullPath,
    string DisplayPath,
    bool ExistedBefore,
    Func<string>? ReadContent);

internal sealed class WorkspaceDestinationContentPlacementPolicy
{
    private static readonly Regex TestFrameworkShimNamespaceRegex = new(
        @"\bnamespace\s+(Microsoft\.VisualStudio\.TestTools\.UnitTesting|Xunit|NUnit\.Framework)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ProjectFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".fsproj",
        ".sln",
        ".slnx",
        ".vbproj"
    };

    private static readonly HashSet<string> BuildProjectFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".fsproj",
        ".vbproj"
    };

    private static readonly HashSet<string> ProjectSourceFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".cshtml",
        ".fs",
        ".razor",
        ".vb"
    };

    private readonly WorkspacePathPolicy pathPolicy;
    private readonly Func<string, IReadOnlyList<string>> enumerateProjectFiles;

    public WorkspaceDestinationContentPlacementPolicy(WorkspacePathPolicy pathPolicy)
    {
        this.pathPolicy = pathPolicy ?? throw new ArgumentNullException(nameof(pathPolicy));
        enumerateProjectFiles = EnumerateProjectFiles;
    }

    internal WorkspaceDestinationContentPlacementPolicy(
        WorkspacePathPolicy pathPolicy,
        Func<string, IReadOnlyList<string>> enumerateProjectFiles)
    {
        this.pathPolicy = pathPolicy ?? throw new ArgumentNullException(nameof(pathPolicy));
        this.enumerateProjectFiles = enumerateProjectFiles ?? throw new ArgumentNullException(nameof(enumerateProjectFiles));
    }

    public bool TryValidate(
        WorkspacePathResolution destination,
        string? authorityRootPath,
        IEnumerable<WorkspaceDestinationContentCandidate> candidates,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        message = string.Empty;
        var candidateList = candidates
            .Select(candidate => candidate with
            {
                FullPath = Path.GetFullPath(candidate.FullPath)
            })
            .ToArray();
        if (candidateList.Length == 0)
        {
            return false;
        }

        var authorityRoot = ResolveAuthorityRoot(destination, authorityRootPath);
        if (candidateList.Any(candidate =>
                !pathPolicy.IsPathWithinRoot(candidate.FullPath, authorityRoot)))
        {
            message = "Cannot place content outside the authorized destination root.";
            return true;
        }

        try
        {
            var projectFiles = candidateList.Any(candidate => IsProjectOwnedFile(candidate.FullPath))
                ? enumerateProjectFiles(authorityRoot)
                : [];
            var plannedBuildProjectFiles = candidateList
                .Where(candidate =>
                    !candidate.ExistedBefore &&
                    BuildProjectFileExtensions.Contains(Path.GetExtension(candidate.FullPath)))
                .Select(candidate => candidate.FullPath)
                .ToArray();

            foreach (var candidate in candidateList)
            {
                if (TryGetForbiddenFrameworkShimMessage(candidate, out message))
                {
                    return true;
                }

                if (candidate.ExistedBefore)
                {
                    continue;
                }

                if (TryGetNestedProjectFileMessage(
                        candidate,
                        authorityRoot,
                        plannedBuildProjectFiles,
                        out message) ||
                    TryGetNestedProjectContainerMessage(
                        candidate,
                        authorityRoot,
                        projectFiles,
                        out message))
                {
                    return true;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            message = "Cannot verify the destination project layout because part of the authorized tree is inaccessible; no content changed.";
            return true;
        }
        catch (IOException)
        {
            message = "Cannot verify the destination project layout because part of the authorized tree could not be read; no content changed.";
            return true;
        }

        return false;
    }

    private static bool TryGetForbiddenFrameworkShimMessage(
        WorkspaceDestinationContentCandidate candidate,
        out string message)
    {
        message = string.Empty;
        if (!string.Equals(Path.GetExtension(candidate.FullPath), ".cs", StringComparison.OrdinalIgnoreCase) ||
            candidate.ReadContent is null)
        {
            return false;
        }

        var content = candidate.ReadContent();
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var shimNamespaceMatch = TestFrameworkShimNamespaceRegex.Match(content);
        if (!shimNamespaceMatch.Success)
        {
            return false;
        }

        message = $"Cannot place C# file '{candidate.DisplayPath}' because it defines local shim types in framework or test-framework namespace '{shimNamespaceMatch.Groups[1].Value}'. Do not fake package, runtime, or test APIs to make validation pass. Fix the real package/project references, repair restore/build diagnostics, or return a concrete blocker.";
        return true;
    }

    private bool TryGetNestedProjectFileMessage(
        WorkspaceDestinationContentCandidate candidate,
        string authorityRoot,
        IReadOnlyList<string> plannedBuildProjectFiles,
        out string message)
    {
        message = string.Empty;
        if (!ProjectFileExtensions.Contains(Path.GetExtension(candidate.FullPath)))
        {
            return false;
        }

        var targetDirectoryPath = Path.GetDirectoryName(candidate.FullPath);
        if (string.IsNullOrWhiteSpace(targetDirectoryPath) ||
            ContainsTopLevelProjectFile(targetDirectoryPath))
        {
            return false;
        }

        var plannedAncestor = plannedBuildProjectFiles.FirstOrDefault(projectFile =>
        {
            var projectDirectory = Path.GetDirectoryName(projectFile);
            return !string.IsNullOrWhiteSpace(projectDirectory) &&
                   !pathPolicy.GetPhysicalPathComparer(authorityRoot)
                       .Equals(projectDirectory, targetDirectoryPath) &&
                   pathPolicy.IsPathWithinRoot(targetDirectoryPath, projectDirectory);
        });
        if (!string.IsNullOrWhiteSpace(plannedAncestor))
        {
            message = $"Cannot introduce nested project file '{candidate.DisplayPath}' under another project in the authorized destination tree. Repair the host project in place, or create a sibling project from the host parent directory.";
            return true;
        }

        var currentDirectory = Directory.GetParent(targetDirectoryPath);
        while (currentDirectory is not null &&
               pathPolicy.IsPathWithinRoot(currentDirectory.FullName, authorityRoot))
        {
            if (ContainsTopLevelBuildProjectFile(currentDirectory.FullName))
            {
                message = $"Cannot introduce nested project file '{candidate.DisplayPath}' under an existing .NET project directory. Repair that host in place, or create a sibling project from the host parent directory.";
                return true;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return false;
    }

    private bool TryGetNestedProjectContainerMessage(
        WorkspaceDestinationContentCandidate candidate,
        string authorityRoot,
        IReadOnlyList<string> projectFiles,
        out string message)
    {
        message = string.Empty;
        if (!IsProjectOwnedFile(candidate.FullPath))
        {
            return false;
        }

        var targetDirectory = Path.GetDirectoryName(candidate.FullPath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return false;
        }

        var currentDirectory = Directory.Exists(targetDirectory)
            ? new DirectoryInfo(targetDirectory)
            : Directory.GetParent(targetDirectory);
        while (currentDirectory is not null &&
               pathPolicy.IsPathWithinRoot(currentDirectory.FullName, authorityRoot))
        {
            if (ContainsTopLevelProjectFile(currentDirectory.FullName))
            {
                return false;
            }

            if (TryFindSameNamedNestedProject(currentDirectory.FullName, projectFiles, out var existingProjectFile))
            {
                var existingProjectPath = Path.GetRelativePath(currentDirectory.FullName, existingProjectFile)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                message = $"Cannot place project-owned file '{candidate.DisplayPath}' outside the existing nested project '{existingProjectPath}'. Use the nested host project path, or establish a sibling project through an explicit project-creation operation; do not create a second root project or source tree.";
                return true;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return false;
    }

    private string ResolveAuthorityRoot(
        WorkspacePathResolution destination,
        string? authorityRootPath)
    {
        if (destination.IsWorkspacePath)
        {
            return pathPolicy.WorkspaceRoot;
        }

        if (!string.IsNullOrWhiteSpace(authorityRootPath) &&
            pathPolicy.TryResolveWorkspacePath(
                authorityRootPath,
                allowWorkspaceRoot: true,
                out var authorityResolution,
                out _) &&
            pathPolicy.IsPathWithinRoot(destination.FullPath, authorityResolution.FullPath))
        {
            return authorityResolution.FullPath;
        }

        var targetDirectory = Path.GetDirectoryName(Path.GetFullPath(destination.FullPath));
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Path.GetFullPath(destination.FullPath);
        }

        return Directory.Exists(targetDirectory)
            ? targetDirectory
            : Directory.GetParent(targetDirectory)?.FullName ?? targetDirectory;
    }

    private static bool IsProjectOwnedFile(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        return ProjectFileExtensions.Contains(extension) ||
               ProjectSourceFileExtensions.Contains(extension);
    }

    private bool ContainsTopLevelProjectFile(string directory)
        => ContainsTopLevelFile(directory, ProjectFileExtensions);

    private bool ContainsTopLevelBuildProjectFile(string directory)
        => ContainsTopLevelFile(directory, BuildProjectFileExtensions);

    private bool ContainsTopLevelFile(string directory, IReadOnlySet<string> extensions)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        IPhysicalFileSystemPathPolicy physicalPathPolicy = pathPolicy.GetPhysicalPathPolicy(directory);
        return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Any(path =>
            {
                physicalPathPolicy.EnsureSafePath(path);
                return extensions.Contains(Path.GetExtension(path));
            });
    }

    private bool TryFindSameNamedNestedProject(
        string containerDirectory,
        IReadOnlyList<string> projectFiles,
        out string projectFile)
    {
        projectFile = string.Empty;
        if (!Directory.Exists(containerDirectory))
        {
            return false;
        }

        var containerName = Path.GetFileName(Path.TrimEndingDirectorySeparator(containerDirectory));
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return false;
        }

        foreach (var candidate in projectFiles)
        {
            if (!pathPolicy.IsPathWithinRoot(candidate, containerDirectory))
            {
                continue;
            }

            var projectDirectory = Path.GetDirectoryName(candidate);
            if (string.IsNullOrWhiteSpace(projectDirectory) ||
                pathPolicy.GetPhysicalPathComparer(containerDirectory)
                    .Equals(projectDirectory, containerDirectory))
            {
                continue;
            }

            if (string.Equals(Path.GetFileName(projectDirectory), containerName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(candidate), containerName, StringComparison.OrdinalIgnoreCase))
            {
                projectFile = candidate;
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<string> EnumerateProjectFiles(string authorityRoot)
    {
        if (!Directory.Exists(authorityRoot))
        {
            return [];
        }

        IPhysicalFileSystemPathPolicy physicalPathPolicy = pathPolicy.GetPhysicalPathPolicy(authorityRoot);
        return Directory.EnumerateFiles(
                authorityRoot,
                "*.*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
            })
            .Where(path => ProjectFileExtensions.Contains(Path.GetExtension(path)))
            .Select(path =>
            {
                physicalPathPolicy.EnsureSafePath(path);
                return path;
            })
            .OrderBy(
                path => WorkspacePathPolicy.NormalizeRelativePath(
                    Path.GetRelativePath(authorityRoot, path)),
                StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }
}
