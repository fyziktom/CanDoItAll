using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using ProviderPricingDefaults = CanDoItAll.AgentFramework.Models.ProviderPricingDefaults;

public partial class SettingsPage
{
    private const string FilesSettingsTabKey = "files";

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
    private int providerEditorTabIndex;

    private ConnectorPluginManifest? SelectedProviderManifest => providerManifests.FirstOrDefault(manifest =>
        string.Equals(manifest.PluginKey, providerModel.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<ConfigurationFieldDescriptor> SelectedProviderFields => SelectedProviderManifest?.ConfigurationSchema.Fields ?? [];

    private IReadOnlyList<SecondaryTabItem> SettingsTabs =>
    [
        new("workspace", "Workspace"),
        new("data-sources", "Data Sources"),
        new("storage", "Storage"),
        new(FilesSettingsTabKey, "Files"),
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
        providerManifests = ProviderAdministrationService.ListProviderManifests();
        settingsModel = await WorkspaceService.GetSettingsAsync();
        providers = await ProviderAdministrationService.ListProviderProfilesAsync();
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
            providers = await ProviderAdministrationService.ListProviderProfilesAsync();
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
            var result = await ProviderAdministrationService.SaveProviderAsync(providerModel);
            if (!result.IsSuccess)
            {
                NotificationService.Warning("Provider profile was not saved", string.Join(" ", result.Errors.Select(error => error.Message)));
                return;
            }

            providers = await ProviderAdministrationService.ListProviderProfilesAsync();
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
        providerModel = await ProviderAdministrationService.GetProviderAsync(id);
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
    }

    private async Task TestProviderAsync(Guid id)
    {
        try
        {
            var result = await ProviderAdministrationService.TestProviderAsync(id);
            if (result.Success)
            {
                NotificationService.Success("Provider health check passed", result.Message);
            }
            else
            {
                NotificationService.Warning("Provider health check failed", result.Message);
            }

            providers = await ProviderAdministrationService.ListProviderProfilesAsync();
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider health check failed", exception.Message);
        }
    }

    private async Task RefreshProviderModelPricesAsync()
    {
        try
        {
            NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
            var result = await ProviderAdministrationService.RefreshProviderModelPricesAsync(providerModel);
            if (!result.IsSuccess)
            {
                NotificationService.Warning(
                    "Provider pricing was not loaded",
                    string.Join(" ", result.Errors.Select(error => error.Message)));
                return;
            }

            providerModel.ModelPrices = result.Value!.ModelPrices;
            NotifyPricingRefresh(result.Value);
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider pricing load failed", exception.Message);
        }
    }

    private async Task DeleteProviderAsync(Guid id)
    {
        try
        {
            await ProviderAdministrationService.DeleteProviderAsync(id);
            providers = await ProviderAdministrationService.ListProviderProfilesAsync();
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
        providerEditorTabIndex = 0;
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
        return key is "workspace" or "data-sources" or "storage" or FilesSettingsTabKey or "secrets" or "providers" or "api-access";
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
            ? ProviderConnectorKeys.OpenAi
            : connectorPluginKey.Trim();
        var defaults = ProviderCapabilityDefaults.Resolve(normalizedPluginKey);
        var configuration = BuildDefaultProviderConfiguration(normalizedPluginKey);
        var pricingKind = ResolveAgentFrameworkProviderKind(normalizedPluginKey);
        return new ProviderProfileEditorModel
        {
            ConnectorPluginKey = normalizedPluginKey,
            ConfigSchemaVersion = "1.0",
            IsEnabled = true,
            SupportsStreaming = defaults.SupportsStreaming,
            SupportsToolCalling = defaults.SupportsToolCalling,
            SupportsStructuredOutput = defaults.SupportsStructuredOutput,
            SupportsVision = defaults.SupportsVision,
            Configuration = configuration,
            IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(pricingKind, null),
            ModelPrices = ProviderPricingDefaults.CreateDefaultEditorModels(
                pricingKind,
                configuration.GetText(ProviderConnectorFieldKeys.DefaultModel))
        };
    }

    private void NormalizeProviderEditorForCurrentPlugin(bool resetCapabilities)
    {
        var manifest = providerManifests.FirstOrDefault(candidate =>
            string.Equals(candidate.PluginKey, providerModel.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
        {
            providerModel.ConfigSchemaVersion = string.Empty;
            return;
        }

        providerModel.ConnectorPluginKey = manifest.PluginKey;
        providerModel.ConfigSchemaVersion = manifest.ConfigurationSchema.Version;

        var existingConfiguration = providerModel.Configuration?.Clone() ?? new ConnectorConfigState();
        var mergedConfiguration = BuildDefaultProviderConfiguration(manifest.PluginKey);
        if (!resetCapabilities)
        {
            foreach (var field in manifest.ConfigurationSchema.Fields)
            {
                var existingValue = existingConfiguration.GetText(field.Key);
                if (!string.IsNullOrWhiteSpace(existingValue))
                {
                    mergedConfiguration.SetText(field.Key, existingValue);
                }
            }
        }

        mergedConfiguration.KeepOnly(manifest.ConfigurationSchema.Fields.Select(field => field.Key));
        providerModel.Configuration = mergedConfiguration;
        NormalizeProviderPricingForCurrentPlugin(manifest.PluginKey, resetCapabilities);

        if (!resetCapabilities)
        {
            return;
        }

        var defaults = ProviderCapabilityDefaults.Resolve(manifest.PluginKey);
        providerModel.SupportsStreaming = defaults.SupportsStreaming;
        providerModel.SupportsToolCalling = defaults.SupportsToolCalling;
        providerModel.SupportsStructuredOutput = defaults.SupportsStructuredOutput;
        providerModel.SupportsVision = defaults.SupportsVision;
    }

    private void NormalizeProviderPricingForCurrentPlugin(string pluginKey, bool resetPricing)
    {
        if (!TryResolveAgentFrameworkProviderKind(pluginKey, out var pricingKind))
        {
            return;
        }
        var defaultModel = providerModel.Configuration.GetText(ProviderConnectorFieldKeys.DefaultModel);
        if (resetPricing || providerModel.ModelPrices.Count == 0)
        {
            providerModel.ModelPrices = ProviderPricingDefaults.CreateDefaultEditorModels(pricingKind, defaultModel);
            providerModel.IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(pricingKind, null);
            return;
        }

        var normalizedPrices = ProviderPricingDefaults.NormalizeModelPrices(
            pricingKind,
            defaultModel,
            ProviderPricingDefaults.FromEditorModels(providerModel.ModelPrices));
        providerModel.ModelPrices = ProviderPricingDefaults.ToEditorModels(normalizedPrices);
        providerModel.IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(
            pricingKind,
            providerModel.IsPrivateProvider);
    }

    private static ConnectorConfigState BuildDefaultProviderConfiguration(string pluginKey)
    {
        return pluginKey switch
        {
            ProviderConnectorKeys.OpenAi => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "https://api.openai.com/v1/models",
                [ProviderConnectorFieldKeys.DefaultModel] = ProviderConnectorDefaults.OpenAiModel,
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            }),
            ProviderConnectorKeys.Ollama => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "http://127.0.0.1:11434",
                [ProviderConnectorFieldKeys.DefaultModel] = "llama3.1",
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            }),
            ProviderConnectorKeys.OllamaRemote => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "https://ollama.example.com",
                [ProviderConnectorFieldKeys.DefaultModel] = "llama3.1",
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            }),
            ProviderConnectorKeys.ComfyUi => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "http://127.0.0.1:8188",
                [ProviderConnectorFieldKeys.DefaultModel] = ProviderConnectorDefaults.ComfyUiModel,
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "120",
                [ProviderConnectorFieldKeys.ComfyUiPositivePromptNodeId] = "6",
                [ProviderConnectorFieldKeys.ComfyUiPollIntervalMilliseconds] = "1000"
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

    private static AgentFrameworkProviderKind ResolveAgentFrameworkProviderKind(string? connectorPluginKey)
    {
        return TryResolveAgentFrameworkProviderKind(connectorPluginKey, out var kind)
            ? kind
            : throw new InvalidOperationException($"No provider pricing kind is registered for connector plugin '{connectorPluginKey}'.");
    }

    private static bool TryResolveAgentFrameworkProviderKind(string? connectorPluginKey, out AgentFrameworkProviderKind kind)
    {
        switch (connectorPluginKey?.Trim())
        {
            case ProviderConnectorKeys.ScenarioHarness:
            case ProviderConnectorKeys.ProcessMock:
            case ProviderConnectorKeys.OpenAi:
                kind = AgentFrameworkProviderKind.OpenAi;
                return true;
            case ProviderConnectorKeys.ComfyUi:
                kind = AgentFrameworkProviderKind.ComfyUi;
                return true;
            case ProviderConnectorKeys.Ollama:
            case ProviderConnectorKeys.OllamaRemote:
                kind = AgentFrameworkProviderKind.Ollama;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private bool TryResolveProviderPricingKind(out AgentFrameworkProviderKind pricingKind)
    {
        return TryResolveAgentFrameworkProviderKind(providerModel.ConnectorPluginKey, out pricingKind);
    }

    private string ResolveProviderDefaultModel()
    {
        return providerModel.Configuration.GetText(ProviderConnectorFieldKeys.DefaultModel);
    }

    private void NotifyPricingRefresh(ProviderModelPricingRefreshResult result)
    {
        if (result.ExplicitPriceCount > 0)
        {
            NotificationService.Success("Provider pricing loaded", result.Message);
            return;
        }

        NotificationService.Info("Provider models loaded", result.Message);
    }
}
