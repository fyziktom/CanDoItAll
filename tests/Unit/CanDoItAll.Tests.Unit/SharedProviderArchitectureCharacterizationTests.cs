using System.Xml.Linq;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderArchitectureCharacterizationTests
{
    private static readonly string[] InnerProviderProjects =
    [
        "src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj",
        "src/MAF/Common/CanDoItAll.AgentFramework.Providers/CanDoItAll.AgentFramework.Providers.csproj",
        "src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj"
    ];

    [Fact]
    public void Workspace_provider_row_is_the_canonical_persisted_master()
    {
        var workspaceModels = Read("src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs");
        var registry = Read("src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs");

        Assert.Contains("public sealed class ProviderProfile : IHasConcurrencyToken", workspaceModels, StringComparison.Ordinal);
        Assert.Contains("IEntityTypeConfiguration<ProviderProfile>", workspaceModels, StringComparison.Ordinal);
        Assert.Contains("Workspace_ProviderProfiles", workspaceModels, StringComparison.Ordinal);
        Assert.Contains("dbContext.Set<WorkspaceProviderProfile>()", registry, StringComparison.Ordinal);
        Assert.Contains("await dbContext.SaveChangesAsync(cancellationToken)", registry, StringComparison.Ordinal);
    }

    [Fact]
    public void Inner_provider_projects_do_not_reference_outer_feature_layers()
    {
        string[] forbiddenSegments =
        [
            "CanDoItAll.Modules.Workspace",
            "CanDoItAll.Web",
            "CanDoItAll.Migrations",
            "CanDoItAll.SharedProviders.Http"
        ];

        foreach (var project in InnerProviderProjects)
        {
            var projectText = Read(project);
            Assert.DoesNotContain(
                forbiddenSegments,
                term => projectText.Contains(term, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Source_project_reference_graph_has_no_cycles()
    {
        var root = FindRepositoryRoot();
        var projects = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var graph = projects.ToDictionary(
            project => project,
            project => ReadProjectReferences(project)
                .Where(projects.Contains)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in graph.Keys)
        {
            Visit(project, graph, visiting, visited, []);
        }
    }

    [Fact]
    public void Production_workspace_connector_registration_is_explicit()
    {
        var registration = Read("src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs");
        string[] expectedAdapters =
        [
            "OpenAiProviderAdapter",
            "ScenarioHarnessProviderAdapter",
            "ProcessMockProviderAdapter",
            "ComfyUiProviderAdapter",
            "OllamaProviderAdapter",
            "OllamaRemoteProviderAdapter"
        ];

        foreach (var adapter in expectedAdapters)
        {
            Assert.Contains($"IProviderAdapter, {adapter}", registration, StringComparison.Ordinal);
        }

        Assert.Equal(expectedAdapters.Length, CountOccurrences(registration, "IProviderAdapter,"));
    }

    [Fact]
    public void Azure_is_runtime_metadata_without_a_workspace_connector_manifest()
    {
        var providerKinds = Read("src/MAF/Common/CanDoItAll.AgentFramework.Models/Common/Enums.cs");
        var providerMetadata = Read("src/Modules/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderMetadata.cs");
        var workspaceProviders = Read("src/Modules/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs");

        Assert.Contains("AzureOpenAi", providerKinds, StringComparison.Ordinal);
        Assert.Contains("AgentFrameworkProviderKind.AzureOpenAi => OpenAiProviderAdapter.PluginKey", providerMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("class AzureOpenAiProviderAdapter", workspaceProviders, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_request_contracts_are_not_web_endpoint_parameters()
    {
        var apiRoot = Path.Combine(FindRepositoryRoot(), "src/App/CanDoItAll.Web/Api");
        var apiText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        string[] internalRequestTypes =
        [
            "ProviderChatCompletionRequest",
            "ProviderImageGenerationRequest",
            "ProviderSpeechToTextRequest",
            "ProviderTextToSpeechRequest",
            "ProviderPromptExecutionRequest"
        ];

        Assert.DoesNotContain(
            internalRequestTypes,
            term => apiText.Contains(term, StringComparison.Ordinal));
    }

    [Fact]
    public void Workspace_to_agentframework_mapping_stays_in_the_outer_module()
    {
        var mapper = Read("src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs");
        var mafProject = Read(InnerProviderProjects[2]);

        Assert.Contains("using WorkspaceProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile", mapper, StringComparison.Ordinal);
        Assert.Contains("AgentFrameworkProviderProfile Map(WorkspaceProviderProfile provider)", mapper, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workspace", mafProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_management_component_delegates_storage_and_transport()
    {
        var component = Read("src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs");

        Assert.Contains("WorkspaceService", component, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", component, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDbContext", component, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReadProjectReferences(string project)
    {
        var projectDirectory = Path.GetDirectoryName(project)!;
        return XDocument.Load(project)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
            .ToArray();
    }

    private static void Visit(
        string project,
        IReadOnlyDictionary<string, string[]> graph,
        ISet<string> visiting,
        ISet<string> visited,
        IReadOnlyList<string> path)
    {
        if (visited.Contains(project))
        {
            return;
        }

        if (!visiting.Add(project))
        {
            var cycle = string.Join(" -> ", path.Append(project).Select(Path.GetFileNameWithoutExtension));
            Assert.Fail($"Project-reference cycle detected: {cycle}");
        }

        var nextPath = path.Append(project).ToArray();
        foreach (var dependency in graph[project])
        {
            Visit(dependency, graph, visiting, visited, nextPath);
        }

        visiting.Remove(project);
        visited.Add(project);
    }

    private static int CountOccurrences(string value, string term)
    {
        return value.Split(term, StringSplitOptions.None).Length - 1;
    }

    private static string Read(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
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
