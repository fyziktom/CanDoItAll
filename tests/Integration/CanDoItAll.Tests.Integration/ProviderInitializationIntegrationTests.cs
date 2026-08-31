using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersistedProvider = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using ProviderEditor = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfileEditorModel;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class ProviderInitializationIntegrationTests {
    [Fact]
    public async Task Default_initialization_keeps_existing_seed_and_runtime_fallback_behavior() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var providers = await registry.ListProvidersAsync();
        Assert.Contains(providers, provider => provider.Name == "OpenAI default");
        Assert.Single(providers, provider => provider.Id == ProviderProfileWellKnownIds.RuntimeFallbackOllama);
        Assert.NotNull(await registry.GetProviderAsync(ProviderProfileWellKnownIds.RuntimeFallbackOllama));
        await AssertWorkspaceProviderCountsAsync(scope.ServiceProvider, providers.Count);
    }

    [Fact]
    public async Task Disabled_defaults_stay_empty_and_preserve_manual_provider_after_reinitialization() {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigurationOverrides = new Dictionary<string, string?> {
                [$"{ProviderInitializationOptions.SectionName}:{nameof(ProviderInitializationOptions.SeedDefaults)}"] = "false"
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var loader = scope.ServiceProvider.GetRequiredService<IProviderRuntimeProfileSnapshotLoader>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();
        await using (var db = await dbFactory.CreateDbContextAsync()) {
            Assert.Empty(await db.Set<PersistedProvider>().ToListAsync());
        }
        Assert.Empty(await registry.ListProvidersAsync());
        Assert.Empty(await loader.LoadAllAsync());
        Assert.Null(await registry.GetProviderAsync(ProviderProfileWellKnownIds.RuntimeFallbackOllama));
        Assert.Null(await loader.LoadAsync(ProviderProfileWellKnownIds.RuntimeFallbackOllama));
        await AssertWorkspaceProviderCountsAsync(scope.ServiceProvider, 0);

        var administration = scope.ServiceProvider.GetRequiredService<IProviderAdministrationService>();
        var saved = await administration.SaveProviderAsync(new ProviderEditor {
            Name = "Manually configured Ollama",
            ConnectorPluginKey = ProviderConnectorKeys.Ollama,
            ConfigSchemaVersion = "1.0",
            IsEnabled = true,
            Configuration = new ConnectorConfigState(new Dictionary<string, string> {
                ["baseUrl"] = "http://127.0.0.1:11434",
                ["defaultModel"] = "gemma3:4b",
                ["timeoutSeconds"] = "45"
            })
        });
        Assert.True(saved.IsSuccess);
        await bootstrapper.EnsureCurrentProfileReadyAsync();
        var provider = Assert.Single(await registry.ListProvidersAsync());
        Assert.Equal(saved.Value, provider.Id);
        Assert.Equal("Manually configured Ollama", provider.Name);
        Assert.Equal("gemma3:4b", provider.DefaultModel);
        Assert.Equal("http://127.0.0.1:11434", provider.BaseUrl);
        Assert.Equal(saved.Value, Assert.Single(await loader.LoadAllAsync()).Profile.Id);
        await AssertWorkspaceProviderCountsAsync(scope.ServiceProvider, 1);
    }

    private static async Task AssertWorkspaceProviderCountsAsync(IServiceProvider services, int expectedCount) {
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        Assert.Equal(expectedCount, (await workspace.GetDashboardAsync()).ProviderCount);
        Assert.Equal(expectedCount, (await workspace.GetAgentOverviewAsync()).Totals.ProviderCount);
        Assert.Equal(expectedCount, (await workspace.GetAgentUsageDetailsAsync()).Totals.ProviderCount);
        Assert.Equal(expectedCount, (await workspace.GetProviderUsageDetailsAsync()).Totals.ProviderCount);
        Assert.Equal(expectedCount, (await workspace.GetModelUsageDetailsAsync()).Totals.ProviderCount);
    }
}
