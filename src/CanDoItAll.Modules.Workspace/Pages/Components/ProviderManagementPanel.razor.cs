using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

public partial class ProviderManagementPanel
{
    [Inject]
    public WorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public SecretService SecretService { get; set; } = default!;

    private ProviderProfileEditorModel providerModel = NewProvider();
    private IReadOnlyList<ProviderProfileSummary> providers = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<ConnectorPluginManifest> providerManifests = [];
    private string? providerMessage;
    private string providerSearch = string.Empty;

    private ConnectorPluginManifest? SelectedProviderManifest => providerManifests.FirstOrDefault(manifest =>
            string.Equals(manifest.PluginKey, providerModel.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
        ?? providerManifests.FirstOrDefault();

    private IReadOnlyList<ConnectorConfigFieldDescriptor> SelectedProviderFields => SelectedProviderManifest?.ConfigurationSchema.Fields ?? [];

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
        var result = await WorkspaceService.SaveProviderAsync(providerModel);
        providerMessage = result.IsSuccess
            ? "Provider profile saved."
            : string.Join(" ", result.Errors.Select(error => error.Message));
        if (!result.IsSuccess)
        {
            return;
        }

        providers = await WorkspaceService.ListProviderProfilesAsync();
        await ResetProviderAsync();
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
        var result = await WorkspaceService.TestProviderAsync(id);
        providerMessage = result.Message;
        providers = await WorkspaceService.ListProviderProfilesAsync();
    }

    private async Task DeleteProviderAsync(
        Guid id)
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

    private void NormalizeProviderEditorForCurrentPlugin(
        bool resetCapabilities)
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
            _ => new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
            })
        };
    }

    private static string? ResolveProviderFieldTestId(
        ConnectorConfigFieldDescriptor field)
    {
        return field.Key switch
        {
            ProviderConnectorFieldKeys.BaseUrl => "provider-base-url-input",
            ProviderConnectorFieldKeys.DefaultModel => "provider-default-model-input",
            _ => $"provider-config-{field.Key}"
        };
    }
}
