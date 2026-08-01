using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowArchitectureBoundaryTests
{
    [Fact]
    public void AgentFrameworkCoreDoesNotReferenceMafWorkflowPackage()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Core",
            "CanDoItAll.AgentFramework.Core.csproj");
        var project = XDocument.Load(projectPath);
        var packageReferences = project
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.DoesNotContain(
            "Microsoft.Agents.AI.Workflows",
            packageReferences,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
