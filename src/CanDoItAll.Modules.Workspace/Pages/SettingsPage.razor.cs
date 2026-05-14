using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages;

public partial class SettingsPage
{
    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public IApiTokenService ApiTokenService { get; set; } = default!;

    private WorkspaceSettingsModel settingsModel = new();
    private SecretEditorModel secretModel = NewSecret();
    private ProviderProfileEditorModel providerModel = NewProvider();
    private ApiTokenIssueRequest apiTokenModel = NewApiToken();
    private ApiAccessStatus? apiStatus;
    private ApiTokenIssueResult? issuedApiToken;
    private IReadOnlyList<ProviderProfileSummary> providers = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<ConnectorPluginManifest> providerManifests = [];
    private string settingsTab = "workspace";
    private string secretSearch = string.Empty;
    private string providerSearch = string.Empty;
    private string apiScopesText = "api";

    private ConnectorPluginManifest? SelectedProviderManifest => providerManifests.FirstOrDefault(manifest =>
            string.Equals(manifest.PluginKey, providerModel.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
        ?? providerManifests.FirstOrDefault();

    private IReadOnlyList<ConfigurationFieldDescriptor> SelectedProviderFields => SelectedProviderManifest?.ConfigurationSchema.Fields ?? [];

    private IReadOnlyList<SecondaryTabItem> SettingsTabs =>
    [
        new("workspace", "Workspace"),
        new("data-sources", "Data Sources"),
        new("storage", "Storage"),
        new("secrets", "Secrets", secrets.Count.ToString()),
        new("providers", "Providers", providers.Count.ToString()),
        new("api-access", "API Access", apiStatus?.AuthorizationEnabled == true ? "JWT" : "Open")
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
        apiStatus = ApiTokenService.GetStatus();
        apiTokenModel = NewApiToken(apiStatus?.DefaultTokenLifetimeMinutes);
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
        ApplyRequestedTab();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await WorkspaceService.SaveSettingsAsync(settingsModel);
            providers = await WorkspaceService.ListProviderProfilesAsync();
            NotificationService.Success("Workspace defaults saved", "Workspace defaults saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Workspace defaults save failed", exception.Message);
        }
    }

    private async Task SaveSecretAsync()
    {
        try
        {
            var result = await SecretService.SaveAsync(secretModel);
            if (!result.IsSuccess)
            {
                NotificationService.Warning("Secret was not saved", string.Join(" ", result.Errors.Select(error => error.Message)));
                return;
            }

            secrets = await WorkspaceService.ListSecretsAsync();
            await ResetSecretAsync();
            NotificationService.Success("Secret saved", "Secret saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Secret save failed", exception.Message);
        }
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

        try
        {
            await SecretService.DeleteAsync(secretModel.Id.Value);
            secrets = await WorkspaceService.ListSecretsAsync();
            await ResetSecretAsync();
            NotificationService.Success("Secret deleted", "Secret deleted.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Secret delete failed", exception.Message);
        }
    }

    private Task ResetSecretAsync()
    {
        secretModel = NewSecret();
        return Task.CompletedTask;
    }

    private async Task SaveProviderAsync()
    {
        try
        {
            var result = await WorkspaceService.SaveProviderAsync(providerModel);
            if (!result.IsSuccess)
            {
                NotificationService.Warning("Provider profile was not saved", string.Join(" ", result.Errors.Select(error => error.Message)));
                return;
            }

            providers = await WorkspaceService.ListProviderProfilesAsync();
            await ResetProviderAsync();
            NotificationService.Success("Provider profile saved", "Provider profile saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider save failed", exception.Message);
        }
    }

    private async Task EditProviderAsync(Guid id)
    {
        providerModel = await WorkspaceService.GetProviderAsync(id);
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
    }

    private async Task TestProviderAsync(Guid id)
    {
        try
        {
            var result = await WorkspaceService.TestProviderAsync(id);
            if (result.Success)
            {
                NotificationService.Success("Provider health check passed", result.Message);
            }
            else
            {
                NotificationService.Warning("Provider health check failed", result.Message);
            }

            providers = await WorkspaceService.ListProviderProfilesAsync();
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider health check failed", exception.Message);
        }
    }

    private async Task DeleteProviderAsync(Guid id)
    {
        try
        {
            await WorkspaceService.DeleteProviderAsync(id);
            providers = await WorkspaceService.ListProviderProfilesAsync();
            await ResetProviderAsync();
            NotificationService.Success("Provider profile deleted", "Provider profile deleted.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider delete failed", exception.Message);
        }
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

    private Task IssueApiTokenAsync()
    {
        try
        {
            apiTokenModel.Scopes = ParseApiScopes(apiScopesText);
            issuedApiToken = ApiTokenService.IssueToken(apiTokenModel);
            NotificationService.Success("API token created", $"Token expires at {FormatTimestamp(issuedApiToken.ExpiresAtUtc)}.");
        }
        catch (Exception exception)
        {
            issuedApiToken = null;
            NotificationService.Error("API token failed", exception.Message);
        }

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
        return key is "workspace" or "data-sources" or "storage" or "secrets" or "providers" or "api-access";
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

    private string FormatApiAudience()
    {
        if (apiStatus is null)
        {
            return "API status is not loaded.";
        }

        return apiStatus.AuthorizationEnabled
            ? $"Issuer: {apiStatus.Issuer} / Audience: {apiStatus.Audience}"
            : "Bearer tokens are not required.";
    }

    private static List<string> ParseApiScopes(string value)
    {
        var scopes = value
            .Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return scopes.Count == 0 ? ["api"] : scopes;
    }

    private static SecretEditorModel NewSecret() => new()
    {
        Kind = SecretKind.ApiKey,
        Scope = "workspace"
    };

    private static ApiTokenIssueRequest NewApiToken(int? lifetimeMinutes = null) => new()
    {
        Subject = "api-client",
        DisplayName = "API client",
        LifetimeMinutes = lifetimeMinutes,
        Scopes = ["api"]
    };

    private static ProviderProfileEditorModel NewProvider(string? connectorPluginKey = null)
    {
        var normalizedPluginKey = string.IsNullOrWhiteSpace(connectorPluginKey)
            ? OpenAiProviderAdapter.PluginKey
            : connectorPluginKey.Trim();
        var defaults = WorkspaceProviderCapabilityDefaults.Resolve(normalizedPluginKey);
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

        var defaults = WorkspaceProviderCapabilityDefaults.Resolve(manifest.PluginKey);
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

    private static string? ResolveProviderFieldTestId(ConfigurationFieldDescriptor field)
    {
        return field.Key switch
        {
            ProviderConnectorFieldKeys.BaseUrl => "provider-base-url-input",
            ProviderConnectorFieldKeys.DefaultModel => "provider-default-model-input",
            _ => $"provider-config-{field.Key}"
        };
    }
}
