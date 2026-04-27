using Bunit;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class SettingsPageProvidersTests
{
    private const string ProviderBaseUrlFieldKey = "baseUrl";
    private const string ProviderDefaultModelFieldKey = "defaultModel";
    private const string ProviderTimeoutSecondsFieldKey = "timeoutSeconds";

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
        var providerEditor = await workspaceService.GetProviderAsync(provider.Id);
        Assert.False(providerEditor.SupportsStructuredOutput);
    }

    [Fact]
    public async Task Settings_page_renders_unknown_provider_manifest_fields_through_shared_field_editor()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.AddScoped<IProviderAdapter, UnknownManifestProviderAdapter>();
        });
        var secretService = harness.Context.Services.GetRequiredService<SecretService>();
        var secretResult = await secretService.SaveAsync(new SecretEditorModel
        {
            Name = "Provider shared secret",
            Kind = SecretKind.Generic,
            SecretValue = "provider-secret",
            Scope = "workspace"
        });
        Assert.True(secretResult.IsSuccess);

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=providers");

        var cut = harness.Context.RenderComponent<SettingsPage>();

        cut.Find("[data-testid='provider-plugin-select']").Change(UnknownManifestProviderAdapter.PluginKey);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unknown provider manifest", cut.Markup);
            Assert.Contains("Custom JSON payload", cut.Markup);
            Assert.Contains("Shared secret reference", cut.Markup);
            Assert.Contains(secretResult.Value.ToString("D"), cut.Markup, StringComparison.OrdinalIgnoreCase);
        });

        cut.Find("[data-testid='provider-name-input']").Change("Unknown manifest provider");
        cut.Find("[data-testid='provider-base-url-input']").Change("https://provider.example.com/v1");
        cut.Find("[data-testid='provider-default-model-input']").Change("wave-10");
        cut.Find("[data-testid='provider-config-timeoutSeconds']").Change("45");
        cut.Find("[data-testid='provider-config-enableAuditing']").Change(true);
        cut.Find("[data-testid='provider-config-jsonPayload']").Change("""{"mode":"strict"}""");
        cut.Find("[data-testid='provider-config-sharedSecret']").Change(secretResult.Value.ToString("D"));
        cut.Find("[data-testid='provider-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Provider profile saved.", cut.Markup);
            Assert.Contains("Unknown manifest provider", cut.Markup);
        });

        var workspaceService = harness.Context.Services.GetRequiredService<WorkspaceService>();
        var providers = await workspaceService.ListProviderProfilesAsync();
        var provider = Assert.Single(providers, item => item.Name == "Unknown manifest provider");
        var editor = await workspaceService.GetProviderAsync(provider.Id);

        Assert.Equal(UnknownManifestProviderAdapter.PluginKey, editor.ConnectorPluginKey);
        Assert.Equal("https://provider.example.com/v1", editor.Configuration.GetText(ProviderBaseUrlFieldKey));
        Assert.Equal("wave-10", editor.Configuration.GetText(ProviderDefaultModelFieldKey));
        Assert.Equal(45, editor.Configuration.GetNumber(ProviderTimeoutSecondsFieldKey));
        Assert.True(editor.Configuration.GetBoolean("enableAuditing"));
        Assert.Equal("""{"mode":"strict"}""", editor.Configuration.GetText("jsonPayload"));
        Assert.Equal(secretResult.Value.ToString("D"), editor.Configuration.GetText("sharedSecret"));
    }

    private sealed class UnknownManifestProviderAdapter : IProviderAdapter
    {
        public const string PluginKey = "provider.test-unknown-manifest";

        private static readonly ConnectorPluginManifest PluginManifest = new(
            PluginKey,
            "Unknown provider manifest",
            "1.0.0",
            ConnectorManifestCapability.ProviderExecution,
            new ConnectorConfigurationSchema(
                "1.0",
                [
                    new ConnectorConfigFieldDescriptor(ProviderBaseUrlFieldKey, "Base URL", ConnectorConfigFieldType.Url, true, "Provider endpoint root."),
                    new ConnectorConfigFieldDescriptor(ProviderDefaultModelFieldKey, "Default model", ConnectorConfigFieldType.Text, true, "Model used by default."),
                    new ConnectorConfigFieldDescriptor(ProviderTimeoutSecondsFieldKey, "Timeout", ConnectorConfigFieldType.Number, true, "HTTP timeout in seconds."),
                    new ConnectorConfigFieldDescriptor("enableAuditing", "Enable auditing", ConnectorConfigFieldType.Boolean, false, "Enable auditing for test calls."),
                    new ConnectorConfigFieldDescriptor("jsonPayload", "Custom JSON payload", ConnectorConfigFieldType.Json, false, "Extra JSON payload for the provider."),
                    new ConnectorConfigFieldDescriptor("sharedSecret", "Shared secret reference", ConnectorConfigFieldType.SecretReference, false, "Optional shared secret reference.")
                ]),
            [],
            new ConnectorHealthCheckDescriptor("test", "Exposes every shared connector field type through the provider editor."),
            new ConnectorAgentExposure("workspace.prompt.send", false, false, "Not exposed for tests."),
            null);

        public ConnectorPluginManifest Manifest => PluginManifest;

        public ProviderKind? LegacyProviderKind => null;

        public Task<ProviderHealthResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "Healthy"));
        }

        public Task<Result<ProviderExecutionResponse>> SendAsync(
            ProviderProfile profile,
            ProviderExecutionRequest request,
            string? secretValue,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<ProviderExecutionResponse>.Success(new ProviderExecutionResponse(
                profile.Name,
                profile.DefaultModel,
                "ok",
                request.OutputFormat,
                false)));
        }
    }
}
