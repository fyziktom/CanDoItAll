using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentRuntimeToolProviderArchitectureTests
{
    [Fact]
    public void Tooling_project_does_not_reference_product_modules()
    {
        var project = LoadProject("src", "CanDoItAll.AgentFramework.Tooling", "CanDoItAll.AgentFramework.Tooling.csproj");
        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.DoesNotContain(projectReferences, reference =>
            reference.Contains("CanDoItAll.Modules.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tooling_contracts_do_not_reference_processes_or_modules_namespace()
    {
        var toolingRoot = Path.Combine(FindRepositoryRoot(), "src", "CanDoItAll.AgentFramework.Tooling");
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(toolingRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessesService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessTool", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_and_processes_reference_tooling_without_maf_processes_reference()
    {
        var mafProject = LoadProject("src", "CanDoItAll.AgentFramework.Maf", "CanDoItAll.AgentFramework.Maf.csproj");
        var processesProject = LoadProject("src", "CanDoItAll.Modules.Processes", "CanDoItAll.Modules.Processes.csproj");
        var mafReferences = ProjectReferences(mafProject);

        Assert.Contains(mafReferences, HasToolingReference);
        Assert.DoesNotContain(mafReferences, reference =>
            reference.Contains("CanDoItAll.Modules.Processes", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ProjectReferences(processesProject), HasToolingReference);
    }

    private static bool HasToolingReference(string reference)
    {
        return reference.Contains("CanDoItAll.AgentFramework.Tooling", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ProjectReferences(XDocument project)
    {
        return project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static XDocument LoadProject(params string[] pathParts)
    {
        return XDocument.Load(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
