using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentFrameworkWorkspaceSeedIntegrationTests
{
    [Fact]
    public void Seed_catalog_loads_generic_reconciliation_skill_and_retires_stale_built_in_inline_skills()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var reconciliationCapability = Assert.Single(
            seed.Capabilities,
            item => string.Equals(item.Key, "document-spreadsheet-reconciliation-inline-skill", StringComparison.OrdinalIgnoreCase));
        var retiredCapabilityId = Guid.NewGuid();
        var retiredCapability = new CapabilityCatalogItem(
            retiredCapabilityId,
            CapabilityKind.Skill,
            "retired-built-in-inline-skill",
            "Retired Built-In Inline Skill",
            "Previous built-in inline skill no longer present in the seed catalog.",
            "inline://retired-built-in-inline-skill",
            "{}",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var financialStrategist = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        var spreadsheetAnalyst = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Spreadsheet Analyst", StringComparison.Ordinal));
        var catalog = seed.ToCatalog() with
        {
            Capabilities = seed.Capabilities.Concat([retiredCapability]).ToList(),
            Agents = seed.Agents.Select(agent => agent.Id == financialStrategist.Id
                ? agent with
                {
                    Capabilities = agent.Capabilities.Concat([
                        new AgentCapabilityAssignment(
                            retiredCapabilityId,
                            retiredCapability.Key,
                            retiredCapability.Kind,
                            CapabilityProofStatus.NotRun,
                            null,
                            string.Empty)
                    ]).ToList()
                }
                : agent).ToList()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var normalizedFinancialStrategist = Assert.Single(
            normalized.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));

        Assert.DoesNotContain(normalized.Capabilities, item => item.Id == retiredCapabilityId);
        Assert.DoesNotContain(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == retiredCapabilityId);
        Assert.Contains(spreadsheetAnalyst.Capabilities, item => item.CapabilityId == reconciliationCapability.Id);
        Assert.Contains(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == reconciliationCapability.Id);
    }

    [Fact]
    public async Task Organization_workspace_seeds_playwright_mcp_for_ui_delivery_agents()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var playwrightCapability = Assert.Single(
            capabilities,
            item => item.Kind == CapabilityKind.McpServer &&
                    string.Equals(item.Key, "playwright-local-mcp", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("npx", playwrightCapability.EndpointOrPath);

        using var configuration = JsonDocument.Parse(playwrightCapability.ConfigurationJson);
        var root = configuration.RootElement;
        Assert.Equal("stdio", root.GetProperty("transport").GetString());
        Assert.Equal("npx", root.GetProperty("command").GetString());
        Assert.Equal(".", root.GetProperty("workingDirectory").GetString());
        Assert.Equal("newlineDelimitedJson", root.GetProperty("messageFraming").GetString());
        Assert.Equal("NeverRequire", root.GetProperty("approvalMode").GetString());
        Assert.Contains(
            root.GetProperty("arguments").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "@playwright/mcp@latest", StringComparison.Ordinal));
        var allowedTools = root.GetProperty("allowedTools")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
        Assert.Contains("browser_navigate", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_take_screenshot", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_snapshot", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_console_messages", allowedTools, StringComparer.OrdinalIgnoreCase);

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));

        Assert.Contains(qaAgent.Capabilities, item => item.CapabilityId == playwrightCapability.Id);
        Assert.Contains(programmingAgent.Capabilities, item => item.CapabilityId == playwrightCapability.Id);
    }

    [Fact]
    public async Task Organization_workspace_seeds_openai_image_generation_provider()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var imageProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                    string.Equals(item.Name, "OpenAI image generation", StringComparison.Ordinal));
        var service = new ProviderProfileService();
        var matrix = service.ResolveFeatureMatrix(imageProvider);

        Assert.Equal("https://api.openai.com/v1", imageProvider.BaseUrl);
        Assert.Equal("OPENAI_API_KEY", imageProvider.ApiKeyEnvironmentVariable);
        Assert.Equal("gpt-image-1-mini", imageProvider.DefaultModel);
        Assert.False(imageProvider.SupportsTools);
        Assert.Contains("gpt-image-1-mini", imageProvider.SuggestedModels, StringComparer.OrdinalIgnoreCase);
        Assert.True(matrix.SupportsImageGeneration);
    }

    [Fact]
    public async Task Organization_workspace_seeds_local_comfyui_flux_image_generation_provider()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var imageProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.ComfyUi &&
                    item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                    string.Equals(item.Name, ComfyUiFluxProviderDefaults.ProviderName, StringComparison.Ordinal));
        var service = new ProviderProfileService();
        var matrix = service.ResolveFeatureMatrix(imageProvider);
        using var configuration = JsonDocument.Parse(imageProvider.ConfigurationJson);
        var root = configuration.RootElement;

        Assert.Equal(ComfyUiFluxProviderDefaults.DefaultBaseUrl, imageProvider.BaseUrl);
        Assert.Equal(ComfyUiFluxProviderDefaults.DefaultModel, imageProvider.DefaultModel);
        Assert.True(imageProvider.IsEnabled);
        Assert.True(imageProvider.IsPrivateProvider);
        Assert.False(imageProvider.SupportsTools);
        Assert.Contains(ComfyUiFluxProviderDefaults.DefaultModel, imageProvider.SuggestedModels, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(ComfyUiFluxProviderDefaults.PositivePromptNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.PositivePromptNodeId).GetString());
        Assert.Equal(ComfyUiFluxProviderDefaults.SamplerNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.SamplerNodeId).GetString());
        Assert.Equal(ComfyUiFluxProviderDefaults.LatentSizeNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.WidthNodeId).GetString());
        Assert.Equal(ComfyUiFluxProviderDefaults.OutputNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.OutputNodeId).GetString());
        Assert.Contains("flux1-dev.safetensors", root.GetProperty(ComfyUiProviderConfigurationKeys.WorkflowTemplateJson).GetString(), StringComparison.Ordinal);
        Assert.True(matrix.SupportsImageGeneration);
        Assert.False(matrix.SupportsFunctionTools);
    }

    [Fact]
    public async Task Organization_workspace_seeds_tagged_openai_and_local_ollama_provider_catalog()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefault = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var localOllama = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Local Ollama", StringComparison.Ordinal));
        var remoteOllama = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));

        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, openAiDefault.DefaultModel);
        Assert.Contains("openai", openAiDefault.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("responses", openAiDefault.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("http://127.0.0.1:11434", localOllama.BaseUrl);
        Assert.Equal("llama3.1", localOllama.DefaultModel);
        Assert.Contains("ollama", localOllama.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("local", localOllama.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("remote", remoteOllama.Tags, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Capability_catalog_save_normalizes_and_persists_tags()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilityId = await workspaceService.SaveCapabilityAsync(new CapabilityEditorModel
        {
            Kind = CapabilityKind.McpServer,
            Key = "tagged-mcp-proof",
            Name = "Tagged MCP Proof",
            Description = "Capability tag persistence proof.",
            EndpointOrPath = "npx",
            ConfigurationJson = """{"transport":"logical"}""",
            Tags = ["Economy", "#mcp", "economy"]
        });

        var savedCapability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Id == capabilityId);

        Assert.Equal(["economy", "mcp"], savedCapability.Tags);
    }

    [Fact]
    public async Task Organization_workspace_seeds_screenshot_agent_templates_with_required_access()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefaultProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var openAiImageProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                    string.Equals(item.Name, "OpenAI image generation", StringComparison.Ordinal));
        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var agentsWithTemplates = await workspaceService.ListAgentsAsync(includeTemplates: true);
        var activeAgents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var captureTemplate = Assert.Single(
            agentsWithTemplates,
            item => string.Equals(item.Name, "App Screenshot Capture Agent Template", StringComparison.Ordinal));
        var reviewTemplate = Assert.Single(
            agentsWithTemplates,
            item => string.Equals(item.Name, "Screenshot Review Storage Agent Template", StringComparison.Ordinal));
        var layoutTemplate = Assert.Single(
            agentsWithTemplates,
            item => string.Equals(item.Name, "Layout Image Generation Agent Template", StringComparison.Ordinal));

        Assert.DoesNotContain(activeAgents, item => item.Id == captureTemplate.Id);
        Assert.DoesNotContain(activeAgents, item => item.Id == reviewTemplate.Id);
        Assert.DoesNotContain(activeAgents, item => item.Id == layoutTemplate.Id);

        Assert.True(captureTemplate.IsTemplate);
        Assert.Equal("app-screenshot-capture-agent", captureTemplate.TemplateKey);
        AssertOpenAiBacked(captureTemplate, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertHasCapabilities(
            captureTemplate,
            capabilityIdsByKey["playwright-local-mcp"],
            capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"],
            capabilityIdsByKey["dotnet-app-delivery-inline-skill"],
            capabilityIdsByKey["blazor-ssr-delivery-inline-skill"],
            capabilityIdsByKey["candoitall-watch-playwright-loop"],
            capabilityIdsByKey["workspace-create-directory"],
            capabilityIdsByKey["workspace-write-file"],
            capabilityIdsByKey["workspace-dotnet-run"],
            capabilityIdsByKey["workspace-dotnet-stop"],
            capabilityIdsByKey["workspace-pwsh-run-script"]);

        var captureEditor = await workspaceService.GetAgentEditorAsync(captureTemplate.Id);
        Assert.True(captureEditor.ProjectStructureAccess.CanRead);
        Assert.False(captureEditor.ProjectStructureAccess.CanWrite);
        Assert.True(captureEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(captureEditor.ProcessAccess.CanRead);
        Assert.False(captureEditor.ProcessAccess.CanWrite);
        Assert.True(captureEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, captureEditor.WorkspaceToolAccess.Profile);
        Assert.True(captureEditor.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(captureEditor.WorkspaceToolAccess.CanRunLocalScripts);
        Assert.False(captureEditor.ImageGenerationAccess.CanGenerateImages);
        Assert.False(captureEditor.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.Contains("Start the application once", captureEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Use Playwright MCP", captureEditor.Instructions, StringComparison.Ordinal);

        Assert.True(reviewTemplate.IsTemplate);
        Assert.Equal("screenshot-review-storage-agent", reviewTemplate.TemplateKey);
        AssertOpenAiBacked(reviewTemplate, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertHasCapabilities(
            reviewTemplate,
            capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"],
            capabilityIdsByKey["frontend-skill"],
            capabilityIdsByKey["workspace-create-directory"],
            capabilityIdsByKey["workspace-write-file"],
            capabilityIdsByKey["workspace-source-rag"]);

        var reviewEditor = await workspaceService.GetAgentEditorAsync(reviewTemplate.Id);
        Assert.True(reviewEditor.ProjectStructureAccess.CanRead);
        Assert.True(reviewEditor.ProjectStructureAccess.CanWrite);
        Assert.True(reviewEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(reviewEditor.ProcessAccess.CanRead);
        Assert.True(reviewEditor.ProcessAccess.CanWrite);
        Assert.True(reviewEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, reviewEditor.WorkspaceToolAccess.Profile);
        Assert.True(reviewEditor.WorkspaceToolAccess.CanReadStorage);
        Assert.True(reviewEditor.WorkspaceToolAccess.CanWriteStorage);
        Assert.True(reviewEditor.WorkspaceToolAccess.AllowAllStorageCatalogs);
        Assert.False(reviewEditor.ImageGenerationAccess.CanGenerateImages);
        Assert.True(reviewEditor.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.Contains("project_structure_asset_create", reviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("objectType` `ImageAsset", reviewEditor.Instructions, StringComparison.Ordinal);

        Assert.True(layoutTemplate.IsTemplate);
        Assert.Equal("layout-image-generation-agent", layoutTemplate.TemplateKey);
        AssertOpenAiBacked(layoutTemplate, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertHasCapabilities(
            layoutTemplate,
            capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"],
            capabilityIdsByKey["frontend-skill"],
            capabilityIdsByKey["workspace-create-directory"],
            capabilityIdsByKey["workspace-write-file"],
            capabilityIdsByKey["workspace-source-rag"]);

        var layoutEditor = await workspaceService.GetAgentEditorAsync(layoutTemplate.Id);
        Assert.True(layoutEditor.ProjectStructureAccess.CanRead);
        Assert.True(layoutEditor.ProjectStructureAccess.CanWrite);
        Assert.True(layoutEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(layoutEditor.ProcessAccess.CanRead);
        Assert.True(layoutEditor.ProcessAccess.CanWrite);
        Assert.True(layoutEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, layoutEditor.WorkspaceToolAccess.Profile);
        Assert.True(layoutEditor.ImageGenerationAccess.CanGenerateImages);
        Assert.Equal(openAiImageProvider.Id, layoutEditor.ImageGenerationAccess.PreferredProviderProfileId);
        Assert.Equal("gpt-image-1-mini", layoutEditor.ImageGenerationAccess.DefaultModel);
        Assert.True(layoutEditor.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.Contains("sourceProjectAssets", layoutEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("image_generation_create", layoutEditor.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_default_integrated_agents_do_not_attach_project_structure_or_processes_mcp_capabilities()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        Assert.DoesNotContain(
            capabilities,
            item => item.Kind == CapabilityKind.McpServer &&
                    string.Equals(item.EndpointOrPath, "CanDoItAll.Mcp.Processes", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            capabilities,
            item => item.Kind == CapabilityKind.McpServer &&
                    string.Equals(item.EndpointOrPath, "CanDoItAll.Mcp.ProjectStructure", StringComparison.OrdinalIgnoreCase));

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        foreach (var agentName in new[]
                 {
                     "Portfolio Architect",
                     "Programming Workspace Analyst",
                     "Delivery QA Observer",
                     "Code Review Lead",
                     "UI Review Lead",
                     "Security Reviewer",
                     "Release Readiness Manager",
                     "Delivery Manager",
                     "Research Deep Dive Analyst",
                     ".NET Solution Architect",
                     ".NET Application Developer",
                     "Blazor Application Developer",
                     ".NET QA Review Lead",
                     "JavaScript Solution Architect",
                     "JavaScript Application Developer",
                     "JavaScript QA Review Lead",
                     "Business Strategist",
                     "Financial Strategist",
                     "Marketing Specialist",
                     "Mail Triage Analyst",
                     "Spreadsheet Analyst"
                 })
        {
            var agent = Assert.Single(agents, item => string.Equals(item.Name, agentName, StringComparison.Ordinal));
            Assert.DoesNotContain(
                agent.Capabilities,
                item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                agent.Capabilities,
                item => item.Kind == CapabilityKind.McpServer &&
                        item.CapabilityKey.Contains("process", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Serious_delivery_agents_seed_internal_project_structure_and_process_access_after_mcp_removal()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var architect = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var architectEditor = await workspaceService.GetAgentEditorAsync(architect.Id);
        Assert.True(architectEditor.ProjectStructureAccess.CanRead);
        Assert.True(architectEditor.ProjectStructureAccess.CanWrite);
        Assert.True(architectEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(architectEditor.ProcessAccess.CanRead);
        Assert.False(architectEditor.ProcessAccess.CanWrite);
        Assert.True(architectEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Contains("project_structure_node_delete", architectEditor.Instructions, StringComparison.Ordinal);

        var deliveryManager = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Delivery Manager", StringComparison.Ordinal));
        var deliveryManagerEditor = await workspaceService.GetAgentEditorAsync(deliveryManager.Id);
        Assert.True(deliveryManagerEditor.ProjectStructureAccess.CanRead);
        Assert.True(deliveryManagerEditor.ProjectStructureAccess.CanWrite);
        Assert.True(deliveryManagerEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(deliveryManagerEditor.ProcessAccess.CanRead);
        Assert.False(deliveryManagerEditor.ProcessAccess.CanWrite);
        Assert.True(deliveryManagerEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.BusinessAnalysis, deliveryManagerEditor.WorkspaceToolAccess.Profile);

        foreach (var agentName in new[]
                 {
                     "Programming Workspace Analyst",
                     "Delivery QA Observer",
                     "Code Review Lead",
                     "UI Review Lead",
                     "Security Reviewer",
                     "Release Readiness Manager",
                     "Research Deep Dive Analyst",
                     ".NET Solution Architect",
                     ".NET Application Developer",
                     "Blazor Application Developer",
                     ".NET QA Review Lead",
                     "JavaScript Solution Architect",
                     "JavaScript Application Developer",
                     "JavaScript QA Review Lead",
                     "Business Strategist",
                     "Financial Strategist",
                     "Marketing Specialist",
                     "Mail Triage Analyst",
                     "Spreadsheet Analyst"
                 })
        {
            var agent = Assert.Single(
                await workspaceService.ListAgentsAsync(includeTemplates: false),
                item => string.Equals(item.Name, agentName, StringComparison.Ordinal));
            var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
            Assert.True(editor.ProjectStructureAccess.CanRead);
            Assert.False(editor.ProjectStructureAccess.CanWrite);
            Assert.True(editor.ProjectStructureAccess.AllowAllProjects);
            Assert.True(editor.ProcessAccess.CanRead);
            Assert.False(editor.ProcessAccess.CanWrite);
            Assert.True(editor.ProcessAccess.AllowAllDefinitions);
        }
    }

    [Fact]
    public async Task Organization_workspace_seeds_workspace_source_rag_with_generated_runtime_noise_excluded()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Rag &&
                    string.Equals(item.Key, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));

        using var configuration = JsonDocument.Parse(capability.ConfigurationJson);
        var excludePaths = configuration.RootElement
            .GetProperty("excludePaths")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

        Assert.Contains(".playwright-mcp", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("process-runs", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("data", excludePaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stale_workspace_source_rag_capability_is_refreshed_to_exclude_generated_runtime_noise()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);

        await store.UpdateCatalogAsync(catalog =>
        {
            var downgradedCapabilities = catalog.Capabilities
                .Select(capability =>
                {
                    if (!string.Equals(capability.Key, "workspace-source-rag", StringComparison.OrdinalIgnoreCase))
                    {
                        return capability;
                    }

                    return capability with
                    {
                        ConfigurationJson = JsonSerializer.Serialize(new
                        {
                            ragRoot = ".",
                            extensions = new[] { ".cs", ".md" },
                            excludePaths = new[] { "artifacts", "output" },
                            searchTime = "BeforeAIInvoke",
                            maxResults = 5
                        })
                    };
                })
                .ToList();

            return catalog with
            {
                Capabilities = downgradedCapabilities
            };
        });

        var refreshedCatalog = await store.LoadCatalogAsync();
        var refreshedCapability = Assert.Single(
            refreshedCatalog.Capabilities,
            item => string.Equals(item.Key, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        using var configuration = JsonDocument.Parse(refreshedCapability.ConfigurationJson);
        var excludePaths = configuration.RootElement
            .GetProperty("excludePaths")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

        Assert.Contains(".playwright-mcp", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("process-runs", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("data", excludePaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Organization_workspace_seeds_blazor_ssr_delivery_skill_with_external_target_rules()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Skill &&
                    string.Equals(item.Key, "blazor-ssr-delivery-inline-skill", StringComparison.OrdinalIgnoreCase));
        var instructions = ReadInlineSkillInstructions(capability.ConfigurationJson);

        Assert.Contains("If the project structure or attached step materials name a concrete output directory", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/<drive>/...", instructions, StringComparison.Ordinal);
        Assert.Contains("do not scaffold a parallel copy under `artifacts/...`, `output/...`, or another generated implementation folder", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scaffold directly into it instead of adding an extra nested", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Before any scaffold call", instructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", instructions, StringComparison.Ordinal);
        Assert.Contains("Capture screenshot, browser_snapshot or browser_evaluate state output, and browser_console_messages as current-run evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("When writing xUnit tests, include a visible `using Xunit;`", instructions, StringComparison.Ordinal);
        Assert.Contains("custom route backed only by scaffold-default `app.css` and layout CSS", instructions, StringComparison.Ordinal);
        Assert.Contains("custom class names without matching loaded styles", instructions, StringComparison.Ordinal);
        Assert.Contains("provider-native filenames before managed artifact import", instructions, StringComparison.Ordinal);
        Assert.Contains("one run-app proof node, one run-tests proof node, and one manager summary node", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_seeds_concrete_deliverable_delivery_skill_as_generic_contract()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Skill &&
                    string.Equals(item.Key, "concrete-deliverable-delivery-inline-skill", StringComparison.OrdinalIgnoreCase));
        var instructions = ReadInlineSkillInstructions(capability.ConfigurationJson);

        Assert.Contains("any process step that creates, repairs, validates, or summarizes a concrete deliverable", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A deliverable can be an app, service, API", instructions, StringComparison.Ordinal);
        Assert.Contains("Do not reuse sample topics, older generated apps", instructions, StringComparison.Ordinal);
        Assert.Contains("Use technology-specific skills and tools only after the current files or step contract justify them", instructions, StringComparison.Ordinal);
        Assert.Contains("For documents, render/export/open the produced file", instructions, StringComparison.Ordinal);
        Assert.Contains("For spreadsheets, inspect workbook structure", instructions, StringComparison.Ordinal);
        Assert.Contains("Final delivery order is strict", instructions, StringComparison.Ordinal);
        Assert.Contains("Do not claim completion with chat-only evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("computed styles apply to the primary surface", instructions, StringComparison.Ordinal);
        Assert.Contains("product-specific class names but the accepted screenshot or state output shows only unstyled DOM", instructions, StringComparison.Ordinal);
        Assert.Contains("provider-native filenames before managed artifact import", instructions, StringComparison.Ordinal);
        Assert.Contains("store accepted screenshots as ImageAsset nodes or record the exact project-structure asset handoff", instructions, StringComparison.Ordinal);
        Assert.Contains("one run-app proof node, one run-tests proof node, and one manager summary node", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_seeds_dotnet_app_delivery_skill_with_process_visible_browser_proof()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Skill &&
                    string.Equals(item.Key, "dotnet-app-delivery-inline-skill", StringComparison.OrdinalIgnoreCase));
        var instructions = ReadInlineSkillInstructions(capability.ConfigurationJson);

        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("workspace file tools cannot see the managed browser folder during the same attempt", instructions, StringComparison.Ordinal);
        Assert.Contains("When writing xUnit tests, include a visible `using Xunit;`", instructions, StringComparison.Ordinal);
        Assert.Contains("domain-specific classes but only stock template CSS", instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_stop", instructions, StringComparison.Ordinal);
        Assert.Contains("cleanup.json", instructions, StringComparison.Ordinal);
        Assert.Contains("one run-app proof node, one run-tests proof node, and one manager summary node", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_seeds_serious_delivery_agents_on_openai_with_required_skills()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefaultProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var capabilities = await workspaceService.ListCapabilitiesAsync();
        Assert.DoesNotContain(capabilities, item => string.Equals(item.Key, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(capabilities, item => string.Equals(item.Key, "workspace-inspector-plugin", StringComparison.OrdinalIgnoreCase));
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var bundleWorkflowCapabilityId = capabilityIdsByKey["candoitall-bundle-workflow"];
        var playwrightCapabilityId = capabilityIdsByKey["playwright-local-mcp"];
        var codeanalyticsCapabilityId = capabilityIdsByKey["candoitall-codeanalytics-mcp"];
        var componentsCapabilityId = capabilityIdsByKey["candoitall-components-mcp"];
        var frontendThemeCapabilityId = capabilityIdsByKey["candoitall-frontend-theme"];
        var frontendSkillCapabilityId = capabilityIdsByKey["frontend-skill"];
        var playwrightWorkflowCapabilityId = capabilityIdsByKey["candoitall-watch-playwright-loop"];
        var spreadsheetCapabilityId = capabilityIdsByKey["spreadsheet-skill"];
        var runTestsCapabilityId = capabilityIdsByKey["run-tests"];
        var mstestCapabilityId = capabilityIdsByKey["writing-mstest-tests"];
        var concreteDeliverableDeliveryCapabilityId = capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"];
        var dotnetAppDeliveryCapabilityId = capabilityIdsByKey["dotnet-app-delivery-inline-skill"];
        var blazorSsrDeliveryCapabilityId = capabilityIdsByKey["blazor-ssr-delivery-inline-skill"];
        var workspaceSourceRagCapabilityId = capabilityIdsByKey["workspace-source-rag"];
        var architectureSourceRagCapabilityId = capabilityIdsByKey["architecture-source-rag"];
        var createDirectoryCapabilityId = capabilityIdsByKey["workspace-create-directory"];
        var writeFileCapabilityId = capabilityIdsByKey["workspace-write-file"];
        var appendFileCapabilityId = capabilityIdsByKey["workspace-append-file"];
        var workspaceDotnetRunCapabilityId = capabilityIdsByKey["workspace-dotnet-run"];
        var workspaceDotnetStopCapabilityId = capabilityIdsByKey["workspace-dotnet-stop"];
        var workspaceDotnetNewCapabilityId = capabilityIdsByKey["workspace-dotnet-new"];
        var pwshRunScriptCapabilityId = capabilityIdsByKey["workspace-pwsh-run-script"];
        var convertDocumentCapabilityId = capabilityIdsByKey["workspace-convert-document"];
        var inspectSpreadsheetCapabilityId = capabilityIdsByKey["workspace-inspect-spreadsheet"];
        var inspectImageCapabilityId = capabilityIdsByKey["workspace-inspect-image"];
        var analyzeImageCapabilityId = capabilityIdsByKey["workspace-analyze-image"];
        var analyzeImagesCapabilityId = capabilityIdsByKey["workspace-analyze-images"];
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var architectAgent = Assert.Single(agents, item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var codeReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var uiReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var securityReviewerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var releaseManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));
        var dotnetArchitectAgent = Assert.Single(agents, item => string.Equals(item.Name, ".NET Solution Architect", StringComparison.Ordinal));
        var dotnetDeveloperAgent = Assert.Single(agents, item => string.Equals(item.Name, ".NET Application Developer", StringComparison.Ordinal));
        var blazorDeveloperAgent = Assert.Single(agents, item => string.Equals(item.Name, "Blazor Application Developer", StringComparison.Ordinal));
        var dotnetQaAgent = Assert.Single(agents, item => string.Equals(item.Name, ".NET QA Review Lead", StringComparison.Ordinal));
        var javascriptArchitectAgent = Assert.Single(agents, item => string.Equals(item.Name, "JavaScript Solution Architect", StringComparison.Ordinal));
        var javascriptDeveloperAgent = Assert.Single(agents, item => string.Equals(item.Name, "JavaScript Application Developer", StringComparison.Ordinal));
        var javascriptQaAgent = Assert.Single(agents, item => string.Equals(item.Name, "JavaScript QA Review Lead", StringComparison.Ordinal));
        var businessStrategistAgent = Assert.Single(agents, item => string.Equals(item.Name, "Business Strategist", StringComparison.Ordinal));
        var financialStrategistAgent = Assert.Single(agents, item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        var marketingSpecialistAgent = Assert.Single(agents, item => string.Equals(item.Name, "Marketing Specialist", StringComparison.Ordinal));

        AssertOpenAiBacked(architectAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(programmingAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(qaAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(codeReviewAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(uiReviewAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(securityReviewerAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(releaseManagerAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(dotnetArchitectAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(dotnetDeveloperAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(blazorDeveloperAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(dotnetQaAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(javascriptArchitectAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(javascriptDeveloperAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(javascriptQaAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(businessStrategistAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(financialStrategistAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(marketingSpecialistAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        AssertHasCapabilities(architectAgent, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(programmingAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(qaAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(codeReviewAgent, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(uiReviewAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(securityReviewerAgent, codeanalyticsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(releaseManagerAgent, playwrightCapabilityId, playwrightWorkflowCapabilityId, concreteDeliverableDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(dotnetArchitectAgent, bundleWorkflowCapabilityId, concreteDeliverableDeliveryCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(dotnetDeveloperAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(blazorDeveloperAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(dotnetQaAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(javascriptArchitectAgent, bundleWorkflowCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(javascriptDeveloperAgent, playwrightCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(javascriptQaAgent, playwrightCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(businessStrategistAgent, bundleWorkflowCapabilityId, concreteDeliverableDeliveryCapabilityId, convertDocumentCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(financialStrategistAgent, spreadsheetCapabilityId, concreteDeliverableDeliveryCapabilityId, convertDocumentCapabilityId, inspectSpreadsheetCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(marketingSpecialistAgent, bundleWorkflowCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, convertDocumentCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        Assert.DoesNotContain(architectAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(programmingAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dotnetDeveloperAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(blazorDeveloperAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(javascriptDeveloperAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(businessStrategistAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Organization_workspace_seeds_typed_workspace_tool_profiles_for_delivery_roles()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var programming = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal)).Id);
        var qa = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal)).Id);
        var security = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal)).Id);
        var business = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Business Strategist", StringComparison.Ordinal)).Id);
        var deliveryManager = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Delivery Manager", StringComparison.Ordinal)).Id);
        var research = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal)).Id);

        Assert.Equal(AgentWorkspaceToolProfileKind.SoftwareDevelopment, programming.WorkspaceToolAccess.Profile);
        Assert.True(programming.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(programming.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.True(programming.WorkspaceToolAccess.CanManageWorkspacePaths);

        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, qa.WorkspaceToolAccess.Profile);
        Assert.True(qa.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(qa.WorkspaceToolAccess.CanWriteFiles);
        Assert.False(qa.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.False(qa.WorkspaceToolAccess.CanManageWorkspacePaths);

        Assert.Equal(AgentWorkspaceToolProfileKind.SecurityReview, security.WorkspaceToolAccess.Profile);
        Assert.True(security.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.False(security.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.False(security.WorkspaceToolAccess.CanManageWorkspacePaths);

        Assert.Equal(AgentWorkspaceToolProfileKind.BusinessAnalysis, business.WorkspaceToolAccess.Profile);
        Assert.True(business.WorkspaceToolAccess.CanWriteFiles);
        Assert.True(business.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.False(business.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.False(business.WorkspaceToolAccess.CanScaffoldProjects);

        Assert.Equal(AgentWorkspaceToolProfileKind.BusinessAnalysis, deliveryManager.WorkspaceToolAccess.Profile);
        Assert.True(deliveryManager.WorkspaceToolAccess.CanWriteFiles);
        Assert.True(deliveryManager.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.False(deliveryManager.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.False(deliveryManager.WorkspaceToolAccess.CanScaffoldProjects);

        Assert.Equal(AgentWorkspaceToolProfileKind.ReadOnly, research.WorkspaceToolAccess.Profile);
        Assert.True(research.WorkspaceToolAccess.CanReadFiles);
        Assert.False(research.WorkspaceToolAccess.CanWriteFiles);
        Assert.False(research.WorkspaceToolAccess.CanRunValidationCommands);
    }

    [Fact]
    public async Task Programming_agent_seed_instructions_require_modern_mstest_assertions_for_scaffolded_projects()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var programmingAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(programmingAgent.Id);

        Assert.Contains("dotnet new mstest", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Assert.Throws<T>", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Assert.ThrowsExactly<T>", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Assert.ThrowsException", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("[ExpectedException]", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not introduce legacy", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("expected pre-bootstrap state rather than a blocker", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Run the provided bootstrap or init script first", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("If a required build, test, or browser validation fails", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not create Razor components whose type names collide with domain services, value types, or enums", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("default Bootstrap-looking page structure", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("keep the on-disk solution, project, and folder names short", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-theme", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-skill", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_read", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("source-of-truth app root", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/<drive>/...", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not treat managed `artifacts/...`, `output/...`, or execution-run folders as the product working directory", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("scaffold directly into that directory instead of creating an extra nested app folder", editor.Instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Portfolio_architect_seed_instructions_define_typed_project_structure_blocks_and_spacing_rules()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var architectAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(architectAgent.Id);

        Assert.Contains("typed nodes for their real job", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Feature block", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Architecture block", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Project block", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Work item", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not invent enum names like `FeatureBlock`", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("`ProjectBlock` + `feature`", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("`WorkItem` + `task`", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("siblings should usually be separated by about 280", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("child branches by about 480", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_node_move", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("run subtree recomposition", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("concrete external output path", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolved working directory", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not substitute managed `artifacts/...`, `output/...`, or execution-run evidence roots for the app directory", editor.Instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Serious_delivery_review_and_validation_agents_require_durable_file_writes_in_their_seed_instructions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var codeReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var uiReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var securityReviewerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var releaseManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));

        var codeReviewEditor = await workspaceService.GetAgentEditorAsync(codeReviewAgent.Id);
        var qaEditor = await workspaceService.GetAgentEditorAsync(qaAgent.Id);
        var uiReviewEditor = await workspaceService.GetAgentEditorAsync(uiReviewAgent.Id);
        var securityEditor = await workspaceService.GetAgentEditorAsync(securityReviewerAgent.Id);
        var releaseEditor = await workspaceService.GetAgentEditorAsync(releaseManagerAgent.Id);

        Assert.Contains("workspace_write_file", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("component-library", codeReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("component-library", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-theme", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-skill", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("When Playwright or screenshot review exposes a defect", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not assume legacy route names from earlier sample runs", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("stale evidence", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path-length failures", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Treat untouched scaffold styling, flat stacked forms, or placeholder-looking navigation as QA defects", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("loaded CSS or scoped CSS applies to the primary route classes with computed styles", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("rendered as unstyled DOM is a QA failure", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("meaningful filled, selected, or changed state", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("click a representative sequence", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Blazor render-mode or static-SSR implementation defect", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("If a .NET app is not already running", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("provider/model is not vision-capable", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("A single static screenshot is insufficient", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("If a non-.NET app is not already running and only PowerShell execution is available", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not call `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for non-.NET deliverables", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace file tools cannot see the managed browser folder during the same attempt", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("screenshot asset handoff path and target project node", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("generated delivery workspaces or other non-git execution roots", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("treat them as secondary context only", codeReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path-length failures", codeReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Back every claim with visible proof", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("provider/model is not vision-capable", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("mark conflicting prior screenshots or notes as stale evidence", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Call out flat Bootstrap-default composition, bare stacked form sections, or template navigation chrome", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("backed by loaded CSS or scoped CSS", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("rendered by stock scaffold CSS only", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("stock scaffold", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-theme", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-skill", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current-run evidence", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not accept vague statements like \"secure enough.\"", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Prior-run summaries do not override the current code", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("filesystem assumptions", securityEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keep the decision explicit: ready, blocked, or ready-with-residual-risk", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not accept stale prior-run artifacts as proof for the current release", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("missing active styles for custom visual surfaces", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("For visible browser workflows, release readiness requires process-visible current-run browser artifacts", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("missing, empty, detached, stale, or chat-only browser proof", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("build-system fragility", releaseEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_read", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", releaseEditor.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Specialized_default_agents_have_domain_specific_instructions_for_code_and_business_processes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var dotnetDeveloper = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, ".NET Application Developer", StringComparison.Ordinal)).Id);
        var blazorDeveloper = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Blazor Application Developer", StringComparison.Ordinal)).Id);
        var javascriptDeveloper = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "JavaScript Application Developer", StringComparison.Ordinal)).Id);
        var javascriptQa = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "JavaScript QA Review Lead", StringComparison.Ordinal)).Id);
        var dotnetQa = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, ".NET QA Review Lead", StringComparison.Ordinal)).Id);
        var businessStrategist = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Business Strategist", StringComparison.Ordinal)).Id);
        var financialStrategist = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal)).Id);
        var marketingSpecialist = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Marketing Specialist", StringComparison.Ordinal)).Id);

        Assert.Contains("workspace_dotnet_new", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("checking that the grounded product root is missing or safe to scaffold", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("host and tests as siblings", dotnetDeveloper.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("BaseLib", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("component-library", blazorDeveloper.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Before scaffolding, check the mapped product root", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("small JavaScript interop", blazorDeveloper.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Capture screenshot, browser_snapshot or browser_evaluate state output, and browser_console_messages as current-run evidence", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("package.json", javascriptDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("package manager", javascriptDeveloper.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("For peer review and integration-readiness steps", javascriptDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not satisfy a behavior defect by adding manifests", javascriptDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", dotnetQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", dotnetQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("A single static screenshot is insufficient", dotnetQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not submit Completed or Blocked for missing browser receipts from file inspection alone", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace file tools cannot see the managed browser folder during the same attempt", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("External generated app folders are often not git repositories", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("npm.cmd", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not pass a server implementation script itself to `workspace_pwsh_run_script`", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not use `[System.Threading.Tasks.Task]::Run({ ... })` with scriptblocks", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("use native absolute paths for redirect files", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("do not put `$listener`, `$context`, `$request`, or `$file` variables inside a double-quoted `-Command` string", javascriptQa.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifacts/business/<project-slug>/", businessStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("business-plan.md", businessStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("unit economics", financialStrategist.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assumptions.csv", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("go-to-market", marketingSpecialist.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("campaign-brief.md", marketingSpecialist.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_serious_delivery_seed_agents_are_refreshed_to_the_current_baseline()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefaultProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var openAiChatProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI chat completions", StringComparison.Ordinal));
        var ollamaProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);

        await DowngradeAgentToLegacyQaAsync(workspaceService, capabilityIdsByKey, ollamaProvider.Id);
        await DowngradeAgentToLegacyProgrammingAsync(workspaceService, capabilityIdsByKey, openAiChatProvider.Id);
        await DowngradeAgentToLegacyArchitectAsync(workspaceService, capabilityIdsByKey, openAiDefaultProvider.Id);
        await DowngradeAgentToLegacyCodeReviewAsync(workspaceService, capabilityIdsByKey);
        await DowngradeAgentToLegacyUiReviewAsync(workspaceService, capabilityIdsByKey);
        await DowngradeAgentToLegacySecurityReviewAsync(workspaceService, capabilityIdsByKey);
        await DowngradeAgentToLegacyReleaseReadinessAsync(workspaceService, capabilityIdsByKey);

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var architectAgent = Assert.Single(agents, item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var codeReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var uiReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var securityReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var releaseManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));

        AssertOpenAiBacked(architectAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(programmingAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(qaAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(codeReviewAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(uiReviewAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(securityReviewAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(releaseManagerAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        AssertHasCapabilities(architectAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(programmingAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["run-tests"], capabilityIdsByKey["writing-mstest-tests"], capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"], capabilityIdsByKey["dotnet-app-delivery-inline-skill"], capabilityIdsByKey["blazor-ssr-delivery-inline-skill"], capabilityIdsByKey["workspace-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-dotnet-run"], capabilityIdsByKey["workspace-dotnet-stop"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(qaAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["run-tests"], capabilityIdsByKey["writing-mstest-tests"], capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"], capabilityIdsByKey["dotnet-app-delivery-inline-skill"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-dotnet-run"], capabilityIdsByKey["workspace-dotnet-stop"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(codeReviewAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(uiReviewAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(securityReviewAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(releaseManagerAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["candoitall-bundle-workflow"], capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Stale_research_agent_seed_is_refreshed_and_drops_project_structure_capability()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);

        var researchAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(researchAgent.Id);
        editor.ConfigurationJson = "{}";
        if (capabilityIdsByKey.TryGetValue("project-structure-central", out var projectStructureCapabilityId) &&
            !editor.SelectedCapabilityIds.Contains(projectStructureCapabilityId))
        {
            editor.SelectedCapabilityIds.Add(projectStructureCapabilityId);
        }

        await workspaceService.SaveAgentAsync(editor);

        var refreshedResearchAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal));

        Assert.DoesNotContain(
            refreshedResearchAgent.Capabilities,
            item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(GetExpectedManagedSeedVersion(), refreshedResearchAgent.ConfigurationJson, StringComparison.Ordinal);

        var refreshedEditor = await workspaceService.GetAgentEditorAsync(refreshedResearchAgent.Id);
        Assert.True(refreshedEditor.ProjectStructureAccess.CanRead);
        Assert.True(refreshedEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(refreshedEditor.ProcessAccess.CanRead);
        Assert.True(refreshedEditor.ProcessAccess.AllowAllDefinitions);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_persists_the_refreshed_agent_seed_for_other_processes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        string[] managedAgentNames =
        [
            "Portfolio Architect",
            "Programming Workspace Analyst",
            "Delivery QA Observer",
            "Code Review Lead",
            "UI Review Lead",
            "Security Reviewer",
            "Release Readiness Manager",
            ".NET Solution Architect",
            ".NET Application Developer",
            "Blazor Application Developer",
            ".NET QA Review Lead",
            "JavaScript Solution Architect",
            "JavaScript Application Developer",
            "JavaScript QA Review Lead",
            "Business Strategist",
            "Financial Strategist",
            "Marketing Specialist",
            "Mail Triage Analyst",
            "Spreadsheet Analyst"
        ];

        foreach (var agentName in managedAgentNames)
        {
            MutateAgentSnapshotInCatalog(catalogPath, agentName, "gpt-4o-mini", "{}");
            var staleSnapshot = ReadAgentSnapshotFromCatalog(catalogPath, agentName);
            Assert.Equal("gpt-4o-mini", staleSnapshot.Model);
            Assert.Equal("{}", staleSnapshot.ConfigurationJson);
        }

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        foreach (var agentName in managedAgentNames)
        {
            AssertManagedSeedRefreshed(agentName, ReadAgentSnapshotFromCatalog(catalogPath, agentName));
        }

        var javascriptQaInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "JavaScript QA Review Lead");
        var javascriptDeveloperInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "JavaScript Application Developer");
        var securityReviewerInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "Security Reviewer");
        var releaseManagerInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "Release Readiness Manager");
        Assert.Contains("For peer review and integration-readiness steps", javascriptDeveloperInstructions, StringComparison.Ordinal);
        Assert.Contains("Do not satisfy a behavior defect by adding manifests", javascriptDeveloperInstructions, StringComparison.Ordinal);
        Assert.Contains("single-quoted here-string", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("escape every literal `$`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("must return after it records the reachable URL and process id", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Do not pass a server implementation script itself to `workspace_pwsh_run_script`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("never execute that child server script directly through `workspace_pwsh_run_script`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("do not make missing `package.json` or missing automated tests release-blocking", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("do not call blocking stream reads", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Multiple ambiguous overloads found for \"Run\"", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Treat HTTP reachability as the startup proof", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("single-quoted here-string and launch it with `-File`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("use `browser_evaluate`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("replace it with `browser_evaluate` DOM or state proof", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("identify the shipped entrypoint first", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Do not treat unreferenced source files, stale README claims, or a file manifest as proof of shipped behavior", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Browser proof must exercise the actual loaded runtime", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("inspect the listed artifact paths directly with workspace tools", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("do not recapture fresh browser proof unless the current security step explicitly requires runtime or browser proof", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("Scale security controls to the declared release boundary", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("do not turn public hosting, CI integration, cross-browser support, artifact signing, or production telemetry into release blockers", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("QA evidence names the shipped entrypoint", releaseManagerInstructions, StringComparison.Ordinal);
        Assert.Contains("unreferenced implementation files", releaseManagerInstructions, StringComparison.Ordinal);
        Assert.Contains("Scale release approval and rollout to the declared release boundary", releaseManagerInstructions, StringComparison.Ordinal);
        Assert.Contains("A static/package handoff can complete by confirming the approved artifacts are present in the target root", releaseManagerInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_persists_the_refreshed_blazor_ssr_delivery_capability_for_other_processes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListCapabilitiesAsync();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        var staleConfigurationJson = JsonSerializer.Serialize(new
        {
            key = "blazor-ssr-delivery",
            instructions = "Create or improve Blazor SSR applications with maintainable, strongly typed C# and explicit validation."
        });

        MutateCapabilityConfigurationJsonInCatalog(catalogPath, "blazor-ssr-delivery-inline-skill", staleConfigurationJson);
        var staleConfiguration = ReadCapabilityConfigurationJsonFromCatalog(catalogPath, "blazor-ssr-delivery-inline-skill");
        Assert.DoesNotContain("external-target/<drive>/...", staleConfiguration, StringComparison.Ordinal);

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        var refreshedInstructions = ReadInlineSkillInstructions(
            ReadCapabilityConfigurationJsonFromCatalog(catalogPath, "blazor-ssr-delivery-inline-skill"));
        Assert.Contains("If the project structure or attached step materials name a concrete output directory", refreshedInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/<drive>/...", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("do not scaffold a parallel copy under `artifacts/...`, `output/...`, or another generated implementation folder", refreshedInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scaffold directly into it instead of adding an extra nested", refreshedInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Before any scaffold call", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("When writing xUnit tests, include a visible `using Xunit;`", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("custom route backed only by scaffold-default `app.css` and layout CSS", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("custom class names without matching loaded styles", refreshedInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_refreshes_versioned_inline_skill_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListCapabilitiesAsync();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        var staleConfigurationJson = JsonSerializer.Serialize(new
        {
            skillSource = "inline",
            inlineSkill = new
            {
                name = "architecture-map",
                description = "Outdated task-specific workflow.",
                instructions = "Use this skill only when the user explicitly asks for a Mermaid or class-diagram output."
            }
        });

        MutateCapabilitySnapshotInCatalog(
            catalogPath,
            "architecture-map-inline-skill",
            "Outdated task-specific workflow.",
            staleConfigurationJson);

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        var refreshedSnapshot = ReadCapabilitySnapshotFromCatalog(catalogPath, "architecture-map-inline-skill");
        var seededSnapshot = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Capabilities,
            item => string.Equals(item.Key, "architecture-map-inline-skill", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(seededSnapshot.Description, refreshedSnapshot.Description);
        Assert.Contains(GetExpectedSeriousDeliveryManagedSeedVersion(), refreshedSnapshot.ConfigurationJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_refreshes_versioned_dotnet_tool_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListCapabilitiesAsync();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        var staleConfigurationJson = JsonSerializer.Serialize(new
        {
            tool = "workspace_dotnet_test",
            approvalRequired = false
        });

        MutateCapabilitySnapshotInCatalog(
            catalogPath,
            "workspace-dotnet-test",
            "Runs a bounded dotnet test recipe.",
            staleConfigurationJson);
        MutateCapabilitySnapshotInCatalog(
            catalogPath,
            "workspace-dotnet-stop",
            "Stops a process with a command.",
            staleConfigurationJson);

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        var refreshedSnapshot = ReadCapabilitySnapshotFromCatalog(catalogPath, "workspace-dotnet-test");
        var refreshedStopSnapshot = ReadCapabilitySnapshotFromCatalog(catalogPath, "workspace-dotnet-stop");

        Assert.Contains("stdout/stderr diagnostics", refreshedSnapshot.Description, StringComparison.Ordinal);
        Assert.Contains(GetExpectedSeriousDeliveryManagedSeedVersion(), refreshedSnapshot.ConfigurationJson, StringComparison.Ordinal);
        Assert.Contains("startup.json receipt", refreshedStopSnapshot.Description, StringComparison.Ordinal);
        Assert.Contains("cleanup.json proof", refreshedStopSnapshot.Description, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_stop", refreshedStopSnapshot.ConfigurationJson, StringComparison.Ordinal);
        Assert.Contains(GetExpectedSeriousDeliveryManagedSeedVersion(), refreshedStopSnapshot.ConfigurationJson, StringComparison.Ordinal);
    }

    private static void AssertOpenAiBacked(AgentDefinition agent, Guid providerId, string expectedModel)
    {
        Assert.False(string.IsNullOrWhiteSpace(expectedModel));
        Assert.Equal(providerId, agent.ProviderProfileId);
        Assert.Equal(expectedModel, agent.Model);
    }

    private static void AssertManagedSeedRefreshed(string agentName, (string Model, string ConfigurationJson) snapshot)
    {
        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, snapshot.Model);
        Assert.Contains(GetExpectedManagedSeedVersion(), snapshot.ConfigurationJson, StringComparison.Ordinal);
    }

    private static string GetExpectedManagedSeedVersion()
    {
        var seededAgent = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Agents,
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        using var configuration = JsonDocument.Parse(seededAgent.ConfigurationJson);
        return configuration.RootElement.GetProperty("managedSeedVersion").GetString()
               ?? throw new InvalidOperationException("Managed seed version is missing from the default software delivery agent configuration.");
    }

    private static string GetExpectedSeriousDeliveryManagedSeedVersion()
    {
        var seededCapability = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Capabilities,
            item => string.Equals(item.Key, "dotnet-app-delivery-inline-skill", StringComparison.Ordinal));
        using var configuration = JsonDocument.Parse(seededCapability.ConfigurationJson);
        return configuration.RootElement.GetProperty("managedSeedVersion").GetString()
               ?? throw new InvalidOperationException("Managed seed version is missing from the serious delivery capability configuration.");
    }

    private static void AssertHasCapabilities(AgentDefinition agent, params Guid[] capabilityIds)
    {
        foreach (var capabilityId in capabilityIds)
        {
            Assert.Contains(agent.Capabilities, item => item.CapabilityId == capabilityId);
        }
    }

    private static async Task DowngradeAgentToLegacyQaAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey,
        Guid ollamaProviderId)
    {
        var qaAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(qaAgent.Id);
        editor.Summary = "Tracks what agents are doing, reviews proofs, and highlights missing gates.";
        editor.ProviderProfileId = ollamaProviderId;
        editor.Model = string.Empty;
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["candoitall-watch-playwright-loop"] &&
                         id != capabilityIdsByKey["run-tests"] &&
                         id != capabilityIdsByKey["writing-mstest-tests"] &&
                         id != capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["dotnet-app-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"] &&
                         id != capabilityIdsByKey["workspace-dotnet-run"] &&
                         id != capabilityIdsByKey["workspace-dotnet-stop"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyProgrammingAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey,
        Guid openAiChatProviderId)
    {
        var programmingAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(programmingAgent.Id);
        editor.Summary = "Uses skills, RAG, approval-aware tools, and workspace execution helpers to inspect and improve repositories or build applications.";
        editor.ProviderProfileId = openAiChatProviderId;
        editor.Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-codeanalytics-mcp"] &&
                         id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["run-tests"] &&
                         id != capabilityIdsByKey["candoitall-watch-playwright-loop"] &&
                         id != capabilityIdsByKey["writing-mstest-tests"] &&
                         id != capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["dotnet-app-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"] &&
                         id != capabilityIdsByKey["blazor-ssr-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["workspace-dotnet-run"] &&
                         id != capabilityIdsByKey["workspace-dotnet-stop"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyArchitectAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey,
        Guid openAiDefaultProviderId)
    {
        var architectAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(architectAgent.Id);
        editor.Summary = "Explores integration seams, rights boundaries, and long-term CanDoItAll alignment.";
        editor.ProviderProfileId = openAiDefaultProviderId;
        editor.Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-codeanalytics-mcp"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"] &&
                         id != capabilityIdsByKey["architecture-source-rag"] &&
                         id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyCodeReviewAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyUiReviewAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"] &&
                         id != capabilityIdsByKey["workspace-pwsh-run-script"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacySecurityReviewAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyReleaseReadinessAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"] &&
                         id != capabilityIdsByKey["workspace-pwsh-run-script"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static (string Model, string ConfigurationJson) ReadAgentSnapshotFromCatalog(string catalogPath, string agentName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var agent = document.RootElement.GetProperty("agents")
            .EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("name").GetString(), agentName, StringComparison.Ordinal));

        return (
            agent.GetProperty("model").GetString() ?? string.Empty,
            agent.GetProperty("configurationJson").GetString() ?? string.Empty);
    }

    private static string ReadAgentInstructionsFromCatalog(string catalogPath, string agentName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var agent = document.RootElement.GetProperty("agents")
            .EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("name").GetString(), agentName, StringComparison.Ordinal));

        return agent.GetProperty("instructions").GetString() ?? string.Empty;
    }

    private static string ReadCapabilityConfigurationJsonFromCatalog(string catalogPath, string capabilityKey)
    {
        return ReadCapabilitySnapshotFromCatalog(catalogPath, capabilityKey).ConfigurationJson;
    }

    private static (string Description, string ConfigurationJson) ReadCapabilitySnapshotFromCatalog(string catalogPath, string capabilityKey)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var capability = document.RootElement.GetProperty("capabilities")
            .EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("key").GetString(), capabilityKey, StringComparison.OrdinalIgnoreCase));

        return (
            capability.GetProperty("description").GetString() ?? string.Empty,
            capability.GetProperty("configurationJson").GetString() ?? string.Empty);
    }

    private static string ReadInlineSkillInstructions(string configurationJson)
    {
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement
            .GetProperty("inlineSkill")
            .GetProperty("instructions")
            .GetString()
            ?? string.Empty;
    }

    private static void MutateAgentSnapshotInCatalog(string catalogPath, string agentName, string model, string configurationJson)
    {
        var root = JsonNode.Parse(File.ReadAllText(catalogPath))?.AsObject()
            ?? throw new InvalidOperationException("Catalog JSON could not be parsed.");
        var agents = root["agents"]?.AsArray()
            ?? throw new InvalidOperationException("Catalog JSON did not contain an agents array.");
        var agent = agents
            .OfType<JsonObject>()
            .Single(item => string.Equals(item["name"]?.GetValue<string>(), agentName, StringComparison.Ordinal));

        agent["model"] = model;
        agent["configurationJson"] = configurationJson;
        File.WriteAllText(catalogPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static void MutateCapabilityConfigurationJsonInCatalog(string catalogPath, string capabilityKey, string configurationJson)
    {
        MutateCapabilitySnapshotInCatalog(catalogPath, capabilityKey, description: null, configurationJson);
    }

    private static void MutateCapabilitySnapshotInCatalog(string catalogPath, string capabilityKey, string? description, string configurationJson)
    {
        var root = JsonNode.Parse(File.ReadAllText(catalogPath))?.AsObject()
            ?? throw new InvalidOperationException("Catalog JSON could not be parsed.");
        var capabilities = root["capabilities"]?.AsArray()
            ?? throw new InvalidOperationException("Catalog JSON did not contain a capabilities array.");
        var capability = capabilities
            .OfType<JsonObject>()
            .Single(item => string.Equals(item["key"]?.GetValue<string>(), capabilityKey, StringComparison.OrdinalIgnoreCase));

        if (description is not null)
        {
            capability["description"] = description;
        }

        capability["configurationJson"] = configurationJson;
        File.WriteAllText(catalogPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
