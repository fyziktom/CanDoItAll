using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentRuntimeToolProviderArchitectureTests
{
    private const int ProcessProviderFileMaximumLines = 500;
    private const int ProcessProviderMainFileMaximumLines = 250;
    private const int ProcessProviderMinimumSplitFileCount = 6;

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
        Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessesService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessTool", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_references_tooling_and_only_allowed_module_boundaries()
    {
        var mafProject = LoadProject("src", "CanDoItAll.AgentFramework.Maf", "CanDoItAll.AgentFramework.Maf.csproj");
        var mafReferences = ProjectReferences(mafProject);
        var moduleReferences = mafReferences
            .Where(reference => reference.Contains("CanDoItAll.Modules.", StringComparison.OrdinalIgnoreCase))
            .Select(reference => Path.GetFileNameWithoutExtension(reference))
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Contains(mafReferences, HasToolingReference);
        Assert.Equal(
            [
                "CanDoItAll.Modules.Security",
                "CanDoItAll.Modules.Workspace"
            ],
            moduleReferences);
        Assert.DoesNotContain(mafReferences, reference =>
            reference.Contains("CanDoItAll.Modules.Processes", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(mafReferences, reference =>
            reference.Contains("CanDoItAll.Modules.Projects", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(mafReferences, reference =>
            reference.Contains("CanDoItAll.Modules.Workbench", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Product_runtime_tool_providers_reference_tooling_without_maf_product_references()
    {
        var processesProject = LoadProject("src", "CanDoItAll.Modules.Processes", "CanDoItAll.Modules.Processes.csproj");
        var workbenchProject = LoadProject("src", "CanDoItAll.Modules.Workbench", "CanDoItAll.Modules.Workbench.csproj");
        var agentFrameworkProject = LoadProject("src", "CanDoItAll.Modules.AgentFramework", "CanDoItAll.Modules.AgentFramework.csproj");

        Assert.Contains(ProjectReferences(processesProject), HasToolingReference);
        Assert.Contains(ProjectReferences(workbenchProject), HasToolingReference);
        Assert.Contains(ProjectReferences(agentFrameworkProject), HasToolingReference);
    }

    [Fact]
    public void ProcessAgentRuntimeToolProvider_split_files_stay_below_monolith_threshold()
    {
        var agentToolsRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "AgentTools");
        var providerFiles = Directory
            .EnumerateFiles(agentToolsRoot, "ProcessAgentRuntimeTool*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new
            {
                Name = Path.GetFileName(path),
                Lines = File.ReadLines(path).Count()
            })
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            providerFiles.Length >= ProcessProviderMinimumSplitFileCount,
            $"Expected at least {ProcessProviderMinimumSplitFileCount} process runtime tool provider split files.");
        Assert.All(providerFiles, file =>
            Assert.True(
                file.Lines < ProcessProviderFileMaximumLines,
                $"{file.Name} has {file.Lines} lines and must stay below {ProcessProviderFileMaximumLines}."));

        var mainFile = Assert.Single(providerFiles, file =>
            string.Equals(file.Name, "ProcessAgentRuntimeToolProvider.cs", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            mainFile.Lines < ProcessProviderMainFileMaximumLines,
            $"{mainFile.Name} has {mainFile.Lines} lines and must stay below {ProcessProviderMainFileMaximumLines}.");
    }

    [Fact]
    public void Maf_provider_composition_uses_provider_neutral_names()
    {
        var mafRoot = Path.Combine(FindRepositoryRoot(), "src", "CanDoItAll.AgentFramework.Maf");
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(mafRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("AttachInternalProjectStructureToolsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AttachInternalImageGenerationToolsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateProjectStructureToolBuilder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateImageGenerationToolBuilder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WrapInternalProcessMutationTool", source, StringComparison.Ordinal);
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
