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
        var playwrightCapabilityId = capabilityIdsByKey["playwright-local-mcp"];
        var codeanalyticsCapabilityId = capabilityIdsByKey["candoitall-codeanalytics-mcp"];
        var componentsCapabilityId = capabilityIdsByKey["candoitall-components-mcp"];
        var frontendThemeCapabilityId = capabilityIdsByKey["candoitall-frontend-theme"];
        var frontendSkillCapabilityId = capabilityIdsByKey["frontend-skill"];
        var playwrightWorkflowCapabilityId = capabilityIdsByKey["candoitall-watch-playwright-loop"];
        var runTestsCapabilityId = capabilityIdsByKey["run-tests"];
        var mstestCapabilityId = capabilityIdsByKey["writing-mstest-tests"];
        var blazorSsrDeliveryCapabilityId = capabilityIdsByKey["blazor-ssr-delivery-inline-skill"];
        var workspaceSourceRagCapabilityId = capabilityIdsByKey["workspace-source-rag"];
        var architectureSourceRagCapabilityId = capabilityIdsByKey["architecture-source-rag"];
        var createDirectoryCapabilityId = capabilityIdsByKey["workspace-create-directory"];
        var writeFileCapabilityId = capabilityIdsByKey["workspace-write-file"];
        var appendFileCapabilityId = capabilityIdsByKey["workspace-append-file"];
        var pwshRunScriptCapabilityId = capabilityIdsByKey["workspace-pwsh-run-script"];

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var architectAgent = Assert.Single(agents, item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var codeReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var uiReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var securityReviewerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var releaseManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));

        AssertOpenAiBacked(architectAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(programmingAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(qaAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(codeReviewAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(uiReviewAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(securityReviewerAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(releaseManagerAgent, openAiDefaultProvider.Id, "gpt-4.1");

        AssertHasCapabilities(architectAgent, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(programmingAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(qaAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(codeReviewAgent, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(uiReviewAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(securityReviewerAgent, codeanalyticsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(releaseManagerAgent, playwrightCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
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
        Assert.Contains("Do not create Razor components whose type names collide with domain services, converters, or enums", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("default Bootstrap-looking page structure", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("keep the on-disk solution, project, and folder names short", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-theme", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-skill", editor.Instructions, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("Do not assume legacy route names such as `/length` or `/temperature`", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("stale evidence", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path-length failures", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Treat untouched scaffold styling, flat stacked forms, or placeholder-looking navigation as QA defects", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("filled input state and visible computed result", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("generated delivery workspaces or other non-git execution roots", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("treat them as secondary context only", codeReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path-length failures", codeReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Back every claim with visible proof", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("mark conflicting prior screenshots or notes as stale evidence", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Call out flat Bootstrap-default composition, bare stacked converter sections, or template navigation chrome", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("stock scaffold", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-theme", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontend-skill", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not accept vague statements like \"secure enough.\"", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Prior-run summaries do not override the current code", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("filesystem assumptions", securityEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keep the decision explicit: ready, blocked, or ready-with-residual-risk", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not accept stale prior-run artifacts as proof for the current release", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Treat obviously template-looking UI, unresolved screenshot quality concerns, or ambiguous artifact handoff as release blockers", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("build-system fragility", releaseEditor.Instructions, StringComparison.OrdinalIgnoreCase);
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

        AssertOpenAiBacked(architectAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(programmingAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(qaAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(codeReviewAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(uiReviewAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(securityReviewAgent, openAiDefaultProvider.Id, "gpt-4.1");
        AssertOpenAiBacked(releaseManagerAgent, openAiDefaultProvider.Id, "gpt-4.1");

        AssertHasCapabilities(architectAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(programmingAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["run-tests"], capabilityIdsByKey["writing-mstest-tests"], capabilityIdsByKey["blazor-ssr-delivery-inline-skill"], capabilityIdsByKey["workspace-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(qaAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["run-tests"], capabilityIdsByKey["writing-mstest-tests"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(codeReviewAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(uiReviewAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(securityReviewAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(releaseManagerAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["candoitall-bundle-workflow"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
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
            "Programming Workspace Analyst",
            "Delivery QA Observer",
            "Code Review Lead",
            "UI Review Lead",
            "Security Reviewer",
            "Release Readiness Manager"
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
            AssertManagedSeedRefreshed(ReadAgentSnapshotFromCatalog(catalogPath, agentName));
        }
    }

    private static void AssertOpenAiBacked(AgentDefinition agent, Guid providerId, string expectedModel)
    {
        Assert.Equal(providerId, agent.ProviderProfileId);
        Assert.Equal(expectedModel, agent.Model);
    }

    private static void AssertManagedSeedRefreshed((string Model, string ConfigurationJson) snapshot)
    {
        Assert.Equal("gpt-4.1", snapshot.Model);
        Assert.Contains("2026-04-serious-delivery-v18", snapshot.ConfigurationJson, StringComparison.Ordinal);
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
        editor.Model = "qwen3.5:9b";
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["candoitall-watch-playwright-loop"] &&
                         id != capabilityIdsByKey["run-tests"] &&
                         id != capabilityIdsByKey["writing-mstest-tests"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"])
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
        editor.Summary = "Uses skills, RAG, approval-aware tools, and workspace execution helpers to inspect and improve the current repository or build sample applications.";
        editor.ProviderProfileId = openAiChatProviderId;
        editor.Model = "gpt-4o-mini";
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-codeanalytics-mcp"] &&
                         id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["run-tests"] &&
                         id != capabilityIdsByKey["candoitall-watch-playwright-loop"] &&
                         id != capabilityIdsByKey["writing-mstest-tests"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"] &&
                         id != capabilityIdsByKey["blazor-ssr-delivery-inline-skill"])
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
        editor.Model = "gpt-4o-mini";
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
            .Where(id => id != capabilityIdsByKey["workspace-create-directory"] &&
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
}
