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
    public void Provider_management_owns_the_canonical_persisted_master()
    {
        var providerProfile = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Persistence/ProviderProfile.cs");
        var registry = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/DatabaseProviderProfileRegistry.cs");

        Assert.Contains("public sealed class ProviderProfile : IHasConcurrencyToken", providerProfile, StringComparison.Ordinal);
        Assert.Contains("IEntityTypeConfiguration<ProviderProfile>", providerProfile, StringComparison.Ordinal);
        Assert.Contains("Workspace_ProviderProfiles", providerProfile, StringComparison.Ordinal);
        Assert.Contains("dbContext.Set<ProviderProfile>()", registry, StringComparison.Ordinal);
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
    public void Production_provider_management_connector_registration_is_explicit()
    {
        var registration = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Services/ProviderManagementServiceCollectionExtensions.cs");
        string[] expectedConnectors =
        [
            "OpenAiProviderAdministrationConnector",
            "ScenarioHarnessProviderAdministrationConnector",
            "ProcessMockProviderAdministrationConnector",
            "ComfyUiProviderAdministrationConnector",
            "OllamaProviderAdministrationConnector",
            "OllamaRemoteProviderAdministrationConnector"
        ];

        foreach (var connector in expectedConnectors)
        {
            Assert.Contains($"IProviderAdministrationConnector, {connector}", registration, StringComparison.Ordinal);
        }

        Assert.Equal(expectedConnectors.Length, CountOccurrences(registration, "IProviderAdministrationConnector,"));
    }

    [Fact]
    public void Provider_administration_connectors_do_not_execute_inference()
    {
        var root = FindRepositoryRoot();
        var administrationConnectors = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/ProviderAdministrationConnectors.cs");
        var registration = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Services/ProviderManagementServiceCollectionExtensions.cs");

        Assert.DoesNotContain("ProviderPromptExecutionRequest", administrationConnectors, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderPromptExecutionResponse", administrationConnectors, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync(", administrationConnectors, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderPromptExecutionService", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderHealthCheckService", registration, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            root,
            "src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/LegacyProviderRuntimeGateway.cs")));
    }

    [Fact]
    public void Shared_provider_relay_stays_on_transport_and_MAF_capability_ports()
    {
        var relayApplicationService = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs");
        var relayClient = Read("src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs");
        var runtimeGateway = Read("src/Modules/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderRuntimeGateway.cs");
        var openAiDriver = Read("src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs");
        var ollamaDriver = Read("src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OllamaProviderDriver.cs");
        var imageCapabilityRelay = Read("src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderImageCapabilityRelay.cs");

        Assert.Contains("ISharedProviderRelayDispatcher dispatcher", relayApplicationService, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderAdministrationConnector", relayApplicationService, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderAdministrationConnectorCatalog", relayApplicationService, StringComparison.Ordinal);
        Assert.Contains("IProviderInferenceRelayRuntime inferenceRelayRuntime", relayClient, StringComparison.Ordinal);
        Assert.Contains("inferenceRelayRuntime.SendAsync", relayClient, StringComparison.Ordinal);
        Assert.DoesNotContain("IHttpClientFactory", relayClient, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpRequestMessage", relayClient, StringComparison.Ordinal);
        Assert.Contains("IProviderInferenceRelayRuntime", runtimeGateway, StringComparison.Ordinal);
        Assert.Contains("IProviderInferenceRelayDriver", runtimeGateway, StringComparison.Ordinal);
        Assert.Contains("handle.DispatchAsync", runtimeGateway, StringComparison.Ordinal);
        Assert.Contains("IProviderInferenceRelayDriver", openAiDriver, StringComparison.Ordinal);
        Assert.Contains("IProviderInferenceRelayDriver", ollamaDriver, StringComparison.Ordinal);
        Assert.Contains("IAgentImageGenerationService imageGenerationService", imageCapabilityRelay, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderAdministrationConnector", imageCapabilityRelay, StringComparison.Ordinal);
    }

    [Fact]
    public void Azure_is_runtime_metadata_without_a_dedicated_connector_manifest()
    {
        var providerKinds = Read("src/MAF/Common/CanDoItAll.AgentFramework.Models/Common/Enums.cs");
        var providerMetadata = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/RuntimeProjection/ProviderMetadata.cs");
        var administrationConnectors = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/ProviderAdministrationConnectors.cs");

        Assert.Contains("AzureOpenAi", providerKinds, StringComparison.Ordinal);
        Assert.Contains("AgentFrameworkProviderKind.AzureOpenAi => OpenAiProviderAdministrationConnector.PluginKey", providerMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("class AzureOpenAiProviderAdministrationConnector", administrationConnectors, StringComparison.Ordinal);
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
    public void Persisted_to_runtime_mapping_stays_in_provider_management()
    {
        var mapper = Read("src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/RuntimeProjection/PersistedProviderProfileMapper.cs");
        var mafProject = Read(InnerProviderProjects[2]);

        Assert.Contains("AgentFrameworkProviderProfile Map(ProviderProfile provider)", mapper, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workspace", mafProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Composition_registers_provider_management_once_and_workspace_di_is_provider_free()
    {
        var composition = Read("src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs");
        var workspaceRegistration = Read("src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs");

        Assert.Equal(
            1,
            composition.Split(
                "services.AddAgentFrameworkProviderManagement();",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("AgentFramework.ProviderManagement", workspaceRegistration, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderAdministration", workspaceRegistration, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderRuntime", workspaceRegistration, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderProfile", workspaceRegistration, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedProvider", workspaceRegistration, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_management_component_is_owned_by_agent_framework_and_uses_provider_management()
    {
        var component = Read("src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs");
        var workspaceProject = Read("src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj");
        var workspaceProviderPanel = Path.Combine(
            FindRepositoryRoot(),
            "src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor");

        Assert.Contains("IProviderAdministrationService", component, StringComparison.Ordinal);
        Assert.Contains("IProviderRuntimeAdministrationService", component, StringComparison.Ordinal);
        Assert.DoesNotContain("IAgentFrameworkWorkspaceService", component, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", component, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDbContext", component, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.AgentFramework.ProviderManagement", workspaceProject, StringComparison.Ordinal);
        Assert.False(File.Exists(workspaceProviderPanel));
    }

    [Fact]
    public void History_contracts_do_not_reference_outer_types() {
        var root = FindRepositoryRoot();
        var prefix = "src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.";
        foreach (var layer in new[] { "Abstractions", "Application", "Persistence" }) {
            var project = Path.Combine(root, $"{prefix}{layer}/CanDoItAll.AgentFramework.ProviderHistory.{layer}.csproj");
            Assert.True(File.Exists(project), $"Missing history boundary: {layer}");
            var references = ReadProjectReferences(project).Select(Path.GetFileNameWithoutExtension).ToArray();
            string[] allowed = layer switch {
                "Application" => ["CanDoItAll.AgentFramework.ProviderHistory.Abstractions"],
                "Persistence" => ["CanDoItAll.AgentFramework.ProviderHistory.Abstractions",
                    "CanDoItAll.AgentFramework.ProviderHistory.Application", "CanDoItAll.Infrastructure"],
                _ => []
            };
            Assert.All(references, reference => Assert.Contains(reference, allowed));
        }
        var shared = Read("src/Integration/CanDoItAll.SharedProviders.Abstractions/CanDoItAll.SharedProviders.Abstractions.csproj");
        Assert.DoesNotContain("ProjectReference", shared, StringComparison.Ordinal);
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
