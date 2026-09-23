using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderManagementBoundaryTests
{
    private static readonly string[] ForbiddenProjectNames =
    [
        "CanDoItAll.Modules.AgentFramework.csproj",
        "CanDoItAll.Modules.Workbench.csproj",
        "CanDoItAll.Modules.Workspace.csproj",
        "CanDoItAll.Web.csproj"
    ];

    [Fact]
    public void Provider_management_project_has_no_outer_feature_dependency()
    {
        var root = FindRepositoryRoot();
        var projectDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement");
        var projectPath = Path.Combine(
            projectDirectory,
            "CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj");
        var projectReferences = XDocument
            .Load(projectPath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Path.GetFileName)
            .ToArray();
        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsGeneratedPath(projectDirectory, path))
                .Select(File.ReadAllText));

        Assert.DoesNotContain(
            projectReferences,
            reference => ForbiddenProjectNames.Contains(reference, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain("CanDoItAll.Modules.Workspace", source, StringComparison.Ordinal);
    }

    private static bool IsGeneratedPath(string projectDirectory, string path)
    {
        var relativePath = Path.GetRelativePath(projectDirectory, path);
        var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return firstSegment is "bin" or "obj";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
