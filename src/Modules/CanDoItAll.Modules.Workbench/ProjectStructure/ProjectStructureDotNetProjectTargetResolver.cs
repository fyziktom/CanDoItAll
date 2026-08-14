using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureDotNetProjectTargetResolver
{
    ProjectStructureDotNetProjectTargetResolution Resolve(string path);
}

public sealed record ProjectStructureDotNetProjectTargetResolution(string? ProjectFilePath, string Message)
{
    public bool IsSuccess => !string.IsNullOrWhiteSpace(ProjectFilePath);
}

public sealed class ProjectStructureDotNetProjectTargetResolver(
    FileSystemStoragePathPolicy fileSystemStoragePathPolicy) : IProjectStructureDotNetProjectTargetResolver
{
    public ProjectStructureDotNetProjectTargetResolution Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail("A .NET project path is required.");
        }

        try
        {
            var fullPath = fileSystemStoragePathPolicy.ResolveReparseSafeFullPath(path.Trim());
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(fullPath);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return CreateMissingTargetFailure(fullPath);
            }

            if (!attributes.HasFlag(FileAttributes.Directory))
            {
                return IsSupportedProjectFile(fullPath)
                    ? Success(fullPath)
                    : IsSolutionFile(fullPath)
                        ? Fail("Solution files are not supported as .NET runtime project targets. Set projectPath to the exact application .csproj, .fsproj, or .vbproj file.")
                        : Fail("The configured .NET runtime projectPath is not a supported project file. Set it to an existing .csproj, .fsproj, or .vbproj file.");
            }

            var projectFiles = new List<string>();
            foreach (var candidatePath in Directory
                         .EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
                         .Where(IsSupportedProjectFile)
                         .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                         .ThenBy(path => path, StringComparer.Ordinal))
            {
                var safeCandidatePath = fileSystemStoragePathPolicy.ResolveReparseSafeFullPath(
                    candidatePath);
                var candidateAttributes = File.GetAttributes(safeCandidatePath);
                if (candidateAttributes.HasFlag(FileAttributes.Directory) ||
                    candidateAttributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return Fail("A top-level .NET project candidate is not a regular project file. Replace filesystem links or reparse points with an exact, directly accessible .csproj, .fsproj, or .vbproj file.");
                }

                projectFiles.Add(safeCandidatePath);
            }

            return projectFiles.Count switch
            {
                1 => Success(projectFiles[0]),
                0 => Fail("The configured projectPath directory contains no top-level .NET project file. Inspect the project tree and set projectPath to the exact application project file; nested projects are never selected implicitly."),
                _ => Fail("The configured projectPath directory contains multiple top-level .NET project files. Set projectPath to the exact application project file instead of relying on an ambiguous directory.")
            };
        }
        catch (StorageBrowseException)
        {
            return Fail("The configured .NET runtime projectPath cannot traverse symbolic links or filesystem reparse points.");
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or NotSupportedException
                or PathTooLongException
                or UnauthorizedAccessException)
        {
            return Fail("The configured .NET runtime projectPath could not be inspected. Preserve the current node and report the access blocker instead of saving an unverified target.");
        }
    }

    private static ProjectStructureDotNetProjectTargetResolution CreateMissingTargetFailure(string fullPath)
        => IsSupportedProjectFile(fullPath)
            ? Fail("The configured .NET project file does not exist. Inspect the selected project tree and do not save a runtime repair until the exact application project file is verified.")
            : IsSolutionFile(fullPath)
                ? Fail("Solution files are not supported as .NET runtime project targets. Set projectPath to the exact application .csproj, .fsproj, or .vbproj file.")
                : Fail("The configured .NET runtime projectPath does not exist. Set it to an exact existing project file, or to a directory containing exactly one top-level project file.");

    private static bool IsSupportedProjectFile(string path)
        => Path.GetExtension(path) is { } extension &&
           (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vbproj", StringComparison.OrdinalIgnoreCase));

    private static bool IsSolutionFile(string path)
        => Path.GetExtension(path) is { } extension &&
           (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase));

    private static ProjectStructureDotNetProjectTargetResolution Success(string projectFilePath)
        => new(projectFilePath, "The .NET project target was verified.");

    private static ProjectStructureDotNetProjectTargetResolution Fail(string message)
        => new(null, message);
}
