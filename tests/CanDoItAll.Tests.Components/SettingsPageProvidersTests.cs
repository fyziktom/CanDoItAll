using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class SettingsPageProvidersTests
{
    [Fact]
    public async Task Settings_page_saves_provider_profile_through_manifest_driven_editor()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=providers");

        var cut = harness.Context.RenderComponent<SettingsPage>();

        cut.Find("[data-testid='provider-plugin-select']").Change(OllamaProviderAdapter.PluginKey);
        cut.Find("[data-testid='provider-name-input']").Change("Component Ollama");
        cut.Find("[data-testid='provider-base-url-input']").Change("http://127.0.0.1:11434");
        cut.Find("[data-testid='provider-default-model-input']").Change("llama3.1");
        cut.Find("[data-testid='provider-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Provider profile saved.", cut.Markup);
            Assert.Contains("Component Ollama", cut.Markup);
            Assert.Contains("Ollama local provider", cut.Markup);
        });

        var workspaceService = harness.Context.Services.GetRequiredService<WorkspaceService>();
        var providers = await workspaceService.ListProviderProfilesAsync();
        var provider = Assert.Single(providers, item => item.Name == "Component Ollama");
        Assert.Equal(OllamaProviderAdapter.PluginKey, provider.ConnectorPluginKey);
        Assert.Equal("Ollama local provider", provider.ConnectorDisplayName);
    }
}
