using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowArchitectureBoundaryTests
{
    [Fact]
    public void AgentFrameworkCoreDoesNotReferenceMafWorkflowPackage()
    {
        var root = CanDoItAll.Tests.Support.TestRepositoryRoot.Find();
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
}
