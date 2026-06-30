using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using ProviderPricingDefaults = CanDoItAll.AgentFramework.Models.ProviderPricingDefaults;

public partial class ProviderManagementPanel
{
    [Inject]
    public WorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public SecretService SecretService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private ProviderProfileEditorModel providerModel = NewProvider();
    private IReadOnlyList<ProviderProfileSummary> providers = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<ConnectorPluginManifest> providerManifests = [];
    private string providerSearch = string.Empty;
    private int providerEditorTabIndex;

    private ConnectorPluginManifest? SelectedProviderManifest => providerManifests.FirstOrDefault(manifest =>
        string.Equals(manifest.PluginKey, providerModel.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<ConfigurationFieldDescriptor> SelectedProviderFields => SelectedProviderManifest?.ConfigurationSchema.Fields ?? [];

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

    private async Task LoadAsync()
    {
        providerManifests = WorkspaceService.ListProviderManifests();
        providers = await WorkspaceService.ListProviderProfilesAsync();
        secrets = await WorkspaceService.ListSecretsAsync();
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
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

    private async Task EditProviderAsync(
        Guid id)
    {
        providerModel = await WorkspaceService.GetProviderAsync(id);
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
    }

    private async Task TestProviderAsync(
        Guid id)
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

    private async Task RefreshProviderModelPricesAsync()
    {
        try
        {
            NormalizeProviderEditorForCurrentPlugin(resetCapabilities: false);
            var result = await WorkspaceService.RefreshProviderModelPricesAsync(providerModel);
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

    private async Task DeleteProviderAsync(
        Guid id)
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
        providerEditorTabIndex = 0;
        return Task.CompletedTask;
    }

    private Task HandleProviderPluginChangedAsync()
    {
        NormalizeProviderEditorForCurrentPlugin(resetCapabilities: true);
        return Task.CompletedTask;
    }

    private void ResetProviderSearch()
    {
        providerSearch = string.Empty;
    }

    private static ProviderProfileEditorModel NewProvider(
        string? connectorPluginKey = null)
    {
        var normalizedPluginKey = string.IsNullOrWhiteSpace(connectorPluginKey)
            ? OpenAiProviderAdapter.PluginKey
            : connectorPluginKey.Trim();
        var defaults = WorkspaceProviderCapabilityDefaults.Resolve(normalizedPluginKey);
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

    private void NormalizeProviderEditorForCurrentPlugin(
        bool resetCapabilities)
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

        var defaults = WorkspaceProviderCapabilityDefaults.Resolve(manifest.PluginKey);
        providerModel.SupportsStreaming = defaults.SupportsStreaming;
        providerModel.SupportsToolCalling = defaults.SupportsToolCalling;
        providerModel.SupportsStructuredOutput = defaults.SupportsStructuredOutput;
        providerModel.SupportsVision = defaults.SupportsVision;
    }

    private void NormalizeProviderPricingForCurrentPlugin(
        string pluginKey,
        bool resetPricing)
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

    private static ConnectorConfigState BuildDefaultProviderConfiguration(
        string pluginKey)
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
            ComfyUiProviderAdapter.PluginKey => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.BaseUrl] = "http://127.0.0.1:8188",
                [ProviderConnectorFieldKeys.DefaultModel] = ComfyUiProviderAdapter.DefaultModel,
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

    private static string? ResolveProviderFieldTestId(
        ConfigurationFieldDescriptor field)
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
            case ScenarioHarnessProviderAdapter.PluginKey:
            case ProcessMockProviderAdapter.PluginKey:
            case OpenAiProviderAdapter.PluginKey:
                kind = AgentFrameworkProviderKind.OpenAi;
                return true;
            case ComfyUiProviderAdapter.PluginKey:
                kind = AgentFrameworkProviderKind.ComfyUi;
                return true;
            case OllamaProviderAdapter.PluginKey:
            case OllamaRemoteProviderAdapter.PluginKey:
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
