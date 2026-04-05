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
        ApplyProviderConnectorDefaults(providerModel, preserveIdentity: true);
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
        ApplyProviderConnectorDefaults(providerModel, preserveIdentity: true);
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
        ApplyProviderConnectorDefaults(providerModel, preserveIdentity: true);
        return Task.CompletedTask;
    }

    private Task HandleProviderPluginChangedAsync()
    {
        ApplyProviderConnectorDefaults(providerModel, preserveIdentity: true);
        return Task.CompletedTask;
    }

    private Task HandleSettingsTabChanged(string key)
    {
        settingsTab = key;
        Navigation.NavigateTo(BuildSettingsRoute(key), replace: true);
        return Task.CompletedTask;
    }

    private void ApplyRequestedTab()
    {
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
        return normalizedPluginKey switch
        {
            OpenAiProviderAdapter.PluginKey => new ProviderProfileEditorModel
            {
                ProviderKind = ProviderKind.OpenAi,
                ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
                ConfigSchemaVersion = "1.0",
                BaseUrl = "https://api.openai.com/v1/models",
                DefaultModel = "gpt-4.1",
                TimeoutSeconds = 45,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true
            },
            OllamaProviderAdapter.PluginKey => new ProviderProfileEditorModel
            {
                ProviderKind = ProviderKind.OllamaLocal,
                ConnectorPluginKey = OllamaProviderAdapter.PluginKey,
                ConfigSchemaVersion = "1.0",
                BaseUrl = "http://127.0.0.1:11434",
                DefaultModel = "llama3.1",
                TimeoutSeconds = 45,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true
            },
            OllamaRemoteProviderAdapter.PluginKey => new ProviderProfileEditorModel
            {
                ProviderKind = ProviderKind.OllamaRemote,
                ConnectorPluginKey = OllamaRemoteProviderAdapter.PluginKey,
                ConfigSchemaVersion = "1.0",
                BaseUrl = "https://ollama.example.com",
                DefaultModel = "llama3.1",
                TimeoutSeconds = 45,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true
            },
            _ => new ProviderProfileEditorModel
            {
                ConnectorPluginKey = normalizedPluginKey,
                ConfigSchemaVersion = "1.0",
                TimeoutSeconds = 45,
                IsEnabled = true
            }
        };
    }

    private static void ApplyProviderConnectorDefaults(ProviderProfileEditorModel model, bool preserveIdentity)
    {
        var draft = NewProvider(model.ConnectorPluginKey);
        var existingId = model.Id;
        var existingName = model.Name;
        var existingSecretId = model.ApiKeySecretId;
        var existingEnabled = model.IsEnabled;
        var existingExtraSettingsJson = string.IsNullOrWhiteSpace(model.ExtraSettingsJson) ? "{}" : model.ExtraSettingsJson;

        model.ProviderKind = draft.ProviderKind;
        model.ConnectorPluginKey = draft.ConnectorPluginKey;
        model.ConfigSchemaVersion = draft.ConfigSchemaVersion;
        model.BaseUrl = draft.BaseUrl;
        model.DefaultModel = draft.DefaultModel;
        model.TimeoutSeconds = draft.TimeoutSeconds;
        model.SupportsStreaming = draft.SupportsStreaming;
        model.SupportsToolCalling = draft.SupportsToolCalling;
        model.SupportsStructuredOutput = draft.SupportsStructuredOutput;
        model.SupportsVision = draft.SupportsVision;
        model.ExtraSettingsJson = existingExtraSettingsJson;

        if (!preserveIdentity)
        {
            return;
        }

        model.Id = existingId;
        model.Name = existingName;
        model.ApiKeySecretId = existingSecretId;
        model.IsEnabled = existingEnabled;
    }
}
