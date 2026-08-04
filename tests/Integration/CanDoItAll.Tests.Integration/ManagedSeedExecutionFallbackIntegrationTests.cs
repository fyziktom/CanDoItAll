using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ManagedSeedExecutionFallbackIntegrationTests
{
    [Fact]
    public async Task Managed_seed_execution_resolution_stays_on_openai_when_openai_key_is_missing()
    {
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        try
        {
            await using var application = await TestApplication.CreateAsync();
            await using var scope = application.Services.CreateAsyncScope();
            var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
            var workspaceService = workspaceFactory.GetWorkspaceService(workspaceFactory.GetOrganizationScope());
            var qaAgent = Assert.Single(
                await workspaceService.ListAgentsAsync(includeTemplates: false),
                item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
            var executionServiceField = typeof(AgentFrameworkWorkspaceService).GetField(
                "executionService",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("AgentFrameworkWorkspaceService.executionService field was not found.");
            var executionService = executionServiceField.GetValue(workspaceService)
                ?? throw new InvalidOperationException("Execution service was not available.");
            var resolveProviderMethod = executionService.GetType().GetMethod(
                "ResolveProviderForAgentAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ResolveProviderForAgentAsync method was not found.");

            var resolveTask = resolveProviderMethod.Invoke(executionService, [qaAgent, null, CancellationToken.None])
                as Task<ProviderProfile>;
            Assert.NotNull(resolveTask);

            var provider = await resolveTask!;

            Assert.Contains(provider.Name, new[] { "OpenAI default", "OpenAI chat completions" });
            Assert.Equal(ProviderKind.OpenAi, provider.Kind);
            Assert.Equal("https://api.openai.com/v1", provider.BaseUrl);
            Assert.Empty(provider.ApiKeyEnvironmentVariable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
        }
    }

    [Fact]
    public async Task Organization_catalog_repair_preserves_priced_luna_assignment_for_managed_seed_agent()
    {
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        try
        {
            await using var application = await TestApplication.CreateAsync();
            await using var scope = application.Services.CreateAsyncScope();
            var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
            var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var providers = await workspaceService.ListProvidersAsync();
            var openAiDefaultProvider = Assert.Single(
                providers,
                item => item.Kind == ProviderKind.OpenAi &&
                        string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
            var qaAgentBeforeRepair = Assert.Single(
                await workspaceService.ListAgentsAsync(includeTemplates: false),
                item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));

            Assert.Equal(openAiDefaultProvider.Id, qaAgentBeforeRepair.ProviderProfileId);
            Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, openAiDefaultProvider.DefaultModel);
            Assert.Equal(OpenAiModelIds.Gpt56Luna, qaAgentBeforeRepair.Model);
            Assert.True(ProviderPricingDefaults.TryFindPrice(
                openAiDefaultProvider.ModelPrices,
                qaAgentBeforeRepair.Model,
                out _));

            await repairService.EnsureCurrentOrganizationCatalogAsync();

            var providersAfterRepair = await workspaceService.ListProvidersAsync();
            var openAiDefaultProviderAfterRepair = Assert.Single(
                providersAfterRepair,
                item => item.Kind == ProviderKind.OpenAi &&
                        string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
            var qaAgentAfterRepair = Assert.Single(
                await workspaceService.ListAgentsAsync(includeTemplates: false),
                item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));

            Assert.Equal(openAiDefaultProviderAfterRepair.Id, qaAgentAfterRepair.ProviderProfileId);
            Assert.Equal(OpenAiModelIds.Gpt56Luna, qaAgentAfterRepair.Model);
            Assert.True(ProviderPricingDefaults.TryFindPrice(
                openAiDefaultProviderAfterRepair.ModelPrices,
                qaAgentAfterRepair.Model,
                out _));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
        }
    }

    [Fact]
    public async Task Organization_catalog_repair_preserves_managed_seed_agents_on_fallback_provider_with_explicit_override()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync();
        var remoteOllamaProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));
        var qaAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(qaAgent.Id);
        Assert.Equal(AgentReasoningEffortLevel.Medium, editor.ThinkingEffortOverride);

        editor.ProviderProfileId = remoteOllamaProvider.Id;
        editor.Model = remoteOllamaProvider.DefaultModel;
        editor.EnableBackgroundResponses = false;
        editor.ConfigurationJson = ManagedSeedProviderFallbacks.EnableProviderRepairFallbackOverride(editor.ConfigurationJson);
        await workspaceService.SaveAgentAsync(editor);

        await repairService.EnsureCurrentOrganizationCatalogAsync();

        var qaAgentAfterRepair = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));

        Assert.Equal(remoteOllamaProvider.Id, qaAgentAfterRepair.ProviderProfileId);
        Assert.Equal(remoteOllamaProvider.DefaultModel, qaAgentAfterRepair.Model);
        Assert.False(qaAgentAfterRepair.EnableBackgroundResponses);
        Assert.Equal(
            AgentReasoningEffortLevel.Medium,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(
                qaAgentAfterRepair.ConfigurationJson,
                "repaired managed-seed agent"));
    }

    [Fact]
    public async Task Agent_editor_preserves_provider_default_model_selection_for_local_provider()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync();
        var localOllamaProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Local Ollama", StringComparison.Ordinal));
        var financialStrategist = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(financialStrategist.Id);
        editor.ProviderProfileId = localOllamaProvider.Id;
        editor.Model = string.Empty;
        editor.ThinkingEffortOverride = null;
        editor.IsThinkingEffortOverrideEdited = true;

        await workspaceService.SaveAgentAsync(editor);
        await repairService.EnsureCurrentOrganizationCatalogAsync();

        var savedEditor = await workspaceService.GetAgentEditorAsync(financialStrategist.Id);
        var savedAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => item.Id == financialStrategist.Id);

        Assert.Equal(localOllamaProvider.Id, savedEditor.ProviderProfileId);
        Assert.Empty(savedEditor.Model);
        Assert.Null(savedEditor.ThinkingEffortOverride);
        Assert.Equal(localOllamaProvider.Id, savedAgent.ProviderProfileId);
        Assert.Empty(savedAgent.Model);
        Assert.Equal(localOllamaProvider.DefaultModel, ManagedSeedProviderFallbacks.ResolveModel(savedAgent, localOllamaProvider));
    }
}
