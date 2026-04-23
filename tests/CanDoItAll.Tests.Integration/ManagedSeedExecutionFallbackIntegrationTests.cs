using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ManagedSeedExecutionFallbackIntegrationTests
{
    [Fact]
    public async Task Managed_seed_execution_resolution_uses_remote_ollama_when_openai_key_is_missing()
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

            Assert.Equal("Remote Ollama", provider.Name);
            Assert.Equal(ProviderKind.Ollama, provider.Kind);
            Assert.Equal("http://192.168.10.132:11434", provider.BaseUrl);
            Assert.Equal("gptoss32k:latest", provider.DefaultModel);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
        }
    }

    [Fact]
    public async Task Organization_catalog_repair_rewrites_managed_seed_agents_to_remote_ollama_when_openai_key_is_missing()
    {
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        try
        {
            await using var application = await TestApplication.CreateAsync();
            await using var scope = application.Services.CreateAsyncScope();
            var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
            var providerCredentialResolver = scope.ServiceProvider.GetRequiredService<IAgentProviderCredentialResolver>();
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

            Assert.False(providerCredentialResolver.Resolve(openAiDefaultProvider).IsResolved);
            Assert.Equal(openAiDefaultProvider.Id, qaAgentBeforeRepair.ProviderProfileId);
            Assert.Equal("gpt-4.1", qaAgentBeforeRepair.Model);

            await repairService.EnsureCurrentOrganizationCatalogAsync();

            var providersAfterRepair = await workspaceService.ListProvidersAsync();
            var fallbackProvider = Assert.Single(
                providersAfterRepair,
                item => item.Kind == ProviderKind.Ollama &&
                        string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));
            var qaAgentAfterRepair = Assert.Single(
                await workspaceService.ListAgentsAsync(includeTemplates: false),
                item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));

            Assert.Equal(fallbackProvider.Id, qaAgentAfterRepair.ProviderProfileId);
            Assert.Equal("gptoss32k:latest", qaAgentAfterRepair.Model);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
        }
    }
}
