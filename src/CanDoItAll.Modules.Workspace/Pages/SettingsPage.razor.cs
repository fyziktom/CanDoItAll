using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages;

public partial class SettingsPage
{
    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    private WorkspaceSettingsModel settingsModel = new();
    private SecretEditorModel secretModel = NewSecret();
    private ProviderProfileEditorModel providerModel = NewProvider();
    private IReadOnlyList<ProviderProfileSummary> providers = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<ConnectorPluginManifest> providerManifests = [];
    private string? settingsMessage;
    private string? secretMessage;
    private string? providerMessage;
    private string settingsTab = "workspace";
    private string secretSearch = string.Empty;
    private string providerSearch = string.Empty;

    private ConnectorPluginManifest? SelectedProviderManifest => providerManifests.FirstOrDefault(manifest =>
            string.Equals(manifest.PluginKey, providerModel.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
        ?? providerManifests.FirstOrDefault();

    private IReadOnlyList<ConnectorConfigFieldDescriptor> SelectedProviderFields => SelectedProviderManifest?.ConfigurationSchema.Fields ?? [];

    private IReadOnlyList<SecondaryTabItem> SettingsTabs =>
    [
        new("workspace", "Workspace"),
        new("data-sources", "Data Sources"),
        new("storage", "Storage"),
        new("secrets", "Secrets", secrets.Count.ToString()),
        new("providers", "Providers", providers.Count.ToString()),
        new("project-structure", "Project Structure MCP")
    ];

    private IReadOnlyList<SecretListItem> FilteredSecrets => secrets
        .Where(secret =>
            string.IsNullOrWhiteSpace(secretSearch) ||
            secret.Name.Contains(secretSearch, StringComparison.OrdinalIgnoreCase) ||
            secret.Scope.Contains(secretSearch, StringComparison.OrdinalIgnoreCase))
        .OrderBy(secret => secret.Name)
        .ToList();

    private IReadOnlyList<ProviderProfileSummary> FilteredProviders => providers
        .Where(provider =>
            string.IsNullOrWhiteSpace(providerSearch) ||
            provider.Name.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
            provider.ConnectorDisplayName.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
            provider.DefaultModel.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
            provider.BaseUrl.Contains(providerSearch, StringComparison.OrdinalIgnoreCase))
        .OrderBy(provider => provider.Name)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override void OnParametersSet()
    {
        ApplyRequestedTab();
    }

    private async Task LoadAsync()
    {
        providerManifests = WorkspaceService.ListProviderManifests();
        settingsModel = await WorkspaceService.GetSettingsAsync();
        providers = await WorkspaceService.ListProviderProfilesAsync();
        secrets = await WorkspaceService.ListSecretsAsync();
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
        ApplyRequestedTab();
    }

    private async Task SaveSettingsAsync()
    {
        await WorkspaceService.SaveSettingsAsync(settingsModel);
        settingsMessage = "Workspace defaults saved.";
        providers = await WorkspaceService.ListProviderProfilesAsync();
    }

    private async Task SaveSecretAsync()
    {
        var result = await SecretService.SaveAsync(secretModel);
        secretMessage = result.IsSuccess ? "Secret saved." : string.Join(" ", result.Errors.Select(error => error.Message));
        if (!result.IsSuccess)
        {
            return;
        }

        secrets = await WorkspaceService.ListSecretsAsync();
        await ResetSecretAsync();
    }

    private async Task EditSecretAsync(Guid id)
    {
        var model = await SecretService.GetAsync(id);
        if (model is not null)
        {
            secretModel = model;
        }
    }

    private async Task DeleteSecretAsync()
    {
        if (!secretModel.Id.HasValue)
        {
            return;
        }

        await SecretService.DeleteAsync(secretModel.Id.Value);
        secrets = await WorkspaceService.ListSecretsAsync();
        secretMessage = "Secret deleted.";
        await ResetSecretAsync();
    }

    private Task ResetSecretAsync()
    {
        secretModel = NewSecret();
        return Task.CompletedTask;
    }

    private async Task SaveProviderAsync()
    {
        var result = await WorkspaceService.SaveProviderAsync(providerModel);
        providerMessage = result.IsSuccess ? "Provider profile saved." : string.Join(" ", result.Errors.Select(error => error.Message));
        if (!result.IsSuccess)
        {
            return;
        }

        providers = await WorkspaceService.ListProviderProfilesAsync();
        await ResetProviderAsync();
    }

    private async Task EditProviderAsync(Guid id)
    {
        providerModel = await WorkspaceService.GetProviderAsync(id);
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
    }

    private async Task TestProviderAsync(Guid id)
    {
        var result = await WorkspaceService.TestProviderAsync(id);
        providerMessage = result.Message;
        providers = await WorkspaceService.ListProviderProfilesAsync();
    }

    private async Task DeleteProviderAsync(Guid id)
    {
        await WorkspaceService.DeleteProviderAsync(id);
        providers = await WorkspaceService.ListProviderProfilesAsync();
        providerMessage = "Provider profile deleted.";
        await ResetProviderAsync();
    }

    private Task ResetProviderAsync()
    {
        providerModel = NewProvider();
        return Task.CompletedTask;
    }

    private Task HandleProviderPluginChangedAsync()
    {
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: true);
        return Task.CompletedTask;
    }

    private Task HandleSettingsTabChanged(string key)
    {
        if (string.Equals(key, "providers", StringComparison.Ordinal))
        {
            Navigation.NavigateTo("/agents?tab=providers", replace: true);
            return Task.CompletedTask;
        }

        settingsTab = key;
        Navigation.NavigateTo(BuildSettingsRoute(key), replace: true);
        return Task.CompletedTask;
    }

    private void ApplyRequestedTab()
    {
        if (string.Equals(RequestedTab, "providers", StringComparison.Ordinal))
        {
            settingsTab = "providers";
            return;
        }

        if (IsValidSettingsTab(RequestedTab))
        {
            settingsTab = RequestedTab!;
            return;
        }

        if (!IsValidSettingsTab(settingsTab))
        {
            settingsTab = "workspace";
        }
    }

    private static bool IsValidSettingsTab(string? key)
    {
        return key is "workspace" or "data-sources" or "storage" or "secrets" or "providers" or "project-structure";
    }

    private static string BuildSettingsRoute(string key)
    {
        return string.Equals(key, "workspace", StringComparison.Ordinal)
            ? "/settings"
            : $"/settings?tab={Uri.EscapeDataString(key)}";
    }

    private void ResetSecretSearch()
    {
        secretSearch = string.Empty;
    }

    private void ResetProviderSearch()
    {
        providerSearch = string.Empty;
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.LocalDateTime.ToString("g");
    }

    private static SecretEditorModel NewSecret() => new()
    {
        Kind = SecretKind.ApiKey,
        Scope = "workspace"
    };

    private static ProviderProfileEditorModel NewProvider(string? connectorPluginKey = null)
    {
        var normalizedPluginKey = string.IsNullOrWhiteSpace(connectorPluginKey)
            ? OpenAiProviderAdapter.PluginKey
            : connectorPluginKey.Trim();
        var defaults = ResolveProviderCapabilityDefaults(normalizedPluginKey);
        return new ProviderProfileEditorModel
        {
            ConnectorPluginKey = normalizedPluginKey,
            ConfigSchemaVersion = "1.0",
            IsEnabled = true,
            SupportsStreaming = defaults.SupportsStreaming,
            SupportsToolCalling = defaults.SupportsToolCalling,
            SupportsStructuredOutput = defaults.SupportsStructuredOutput,
            SupportsVision = defaults.SupportsVision,
            Configuration = BuildDefaultProviderConfiguration(normalizedPluginKey)
        };
    }

    private void NormalizeProviderEditorForCurrentPlugin(bool resetCapabilities)
    {
        var manifest = providerManifests.FirstOrDefault(candidate =>
                string.Equals(candidate.PluginKey, providerModel.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
            ?? providerManifests.FirstOrDefault();
        if (manifest is null)
        {
            return;
        }

        providerModel.ConnectorPluginKey = manifest.PluginKey;
        providerModel.ConfigSchemaVersion = manifest.ConfigurationSchema.Version;

        var existingConfiguration = providerModel.Configuration?.Clone() ?? new ConnectorConfigState();
        var mergedConfiguration = BuildDefaultProviderConfiguration(manifest.PluginKey);
        foreach (var field in manifest.ConfigurationSchema.Fields)
        {
            var existingValue = existingConfiguration.GetText(field.Key);
            if (!string.IsNullOrWhiteSpace(existingValue))
            {
                mergedConfiguration.SetText(field.Key, existingValue);
            }
        }

        mergedConfiguration.KeepOnly(manifest.ConfigurationSchema.Fields.Select(field => field.Key));
        providerModel.Configuration = mergedConfiguration;

        if (!resetCapabilities)
        {
            return;
        }

        var defaults = ResolveProviderCapabilityDefaults(manifest.PluginKey);
        providerModel.SupportsStreaming = defaults.SupportsStreaming;
        providerModel.SupportsToolCalling = defaults.SupportsToolCalling;
        providerModel.SupportsStructuredOutput = defaults.SupportsStructuredOutput;
        providerModel.SupportsVision = defaults.SupportsVision;
    }

    private static ConnectorConfigState BuildDefaultProviderConfiguration(string pluginKey)
    {
        return pluginKey switch
        {
            OpenAiProviderAdapter.PluginKey => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "https://api.openai.com/v1/models",
                [ProviderConnectorFieldKeys.DefaultModel] = OpenAiProviderAdapter.DefaultModel,
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            }),
            OllamaProviderAdapter.PluginKey => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "http://127.0.0.1:11434",
                [ProviderConnectorFieldKeys.DefaultModel] = "llama3.1",
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            }),
            OllamaRemoteProviderAdapter.PluginKey => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "https://ollama.example.com",
                [ProviderConnectorFieldKeys.DefaultModel] = "llama3.1",
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            }),
            _ => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            })
        };
    }

    private static (bool SupportsStreaming, bool SupportsToolCalling, bool SupportsStructuredOutput, bool SupportsVision) ResolveProviderCapabilityDefaults(string pluginKey)
    {
        return pluginKey switch
        {
            OpenAiProviderAdapter.PluginKey => (true, true, true, false),
            OllamaProviderAdapter.PluginKey => (true, true, true, false),
            OllamaRemoteProviderAdapter.PluginKey => (true, true, true, false),
            _ => (false, false, false, false)
        };
    }

    private static string? ResolveProviderFieldTestId(ConnectorConfigFieldDescriptor field)
    {
        return field.Key switch
        {
            ProviderConnectorFieldKeys.BaseUrl => "provider-base-url-input",
            ProviderConnectorFieldKeys.DefaultModel => "provider-default-model-input",
            _ => $"provider-config-{field.Key}"
        };
    }
}
