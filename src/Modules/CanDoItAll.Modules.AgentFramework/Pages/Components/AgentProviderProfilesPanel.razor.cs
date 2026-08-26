using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

using IProviderAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderAdministrationService;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using ProviderConnectorFieldKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorFieldKeys;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;
using ProviderMetadata = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMetadata;
using ProviderPricingRefreshResult = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderModelPricingRefreshResult;

public partial class AgentProviderProfilesPanel
{
    [Inject]
    public IProviderRuntimeAdministrationService ProviderRuntimeAdministrationService { get; set; } = default!;

    [Inject]
    public IProviderAdministrationService ProviderAdministrationService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private readonly HashSet<string> expandedProviderTreeNodeIds = [];
    private readonly HashSet<string> knownProviderTagNodeIds = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private ProviderProfileEditorModel providerModel = CreateNewProviderEditor();
    private IReadOnlyList<string> providerTagValues = [];
    private string providerSearch = string.Empty;
    private string suggestedModelsText = string.Empty;
    private int providerEditorTabIndex;
    private bool isLoading;
    private bool isBusy;

    private IReadOnlyList<ProviderProfile> FilteredProviders => providers
        .Where(MatchesProviderSearch)
        .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<TreeViewNode> ProviderTreeNodes
        => ProviderProfileTreeNodeBuilder.Build(FilteredProviders, providerModel.Id, expandedProviderTreeNodeIds);

    private IReadOnlyList<string> AvailableProviderTags => providers
        .SelectMany(provider => provider.Tags)
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private bool HasUnavailableSecretReference =>
        !string.IsNullOrWhiteSpace(providerModel.ApiKeyEnvironmentVariable) &&
        !secrets.Any(secret => string.Equals(
            ProviderMetadata.CreateSecretReference(secret.Id),
            providerModel.ApiKeyEnvironmentVariable,
            StringComparison.OrdinalIgnoreCase));

    private bool SelectedProviderIsSourceManaged => providerModel.Id.HasValue &&
        providers.Any(provider =>
            provider.Id == providerModel.Id.Value &&
            string.Equals(
                provider.ConnectorPluginKey,
                ProviderConnectorKeys.SharedImport,
                StringComparison.Ordinal));

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        try
        {
            var providersTask = ProviderRuntimeAdministrationService.ListProvidersAsync();
            var secretsTask = ProviderAdministrationService.ListSecretsAsync();
            await Task.WhenAll(providersTask, secretsTask);
            providers = await providersTask;
            secrets = await secretsTask;
            RefreshProviderTreeExpansionDefaults();

            if (providerModel.Id.HasValue &&
                providers.Any(item => item.Id == providerModel.Id.Value))
            {
                await EditProviderAsync(providerModel.Id.Value);
            }
            else if (providers.Count > 0)
            {
                await EditProviderAsync(providers[0].Id);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider catalog failed", exception.Message);
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task HandleProviderTreeSelectAsync(string nodeId)
    {
        if (ProviderProfileTreeNodeBuilder.TryReadProviderId(nodeId, out var providerId) &&
            providers.Any(item => item.Id == providerId))
        {
            return EditProviderAsync(providerId);
        }

        return Task.CompletedTask;
    }

    private Task HandleProviderTreeToggleAsync(string nodeId)
    {
        if (!expandedProviderTreeNodeIds.Add(nodeId))
        {
            expandedProviderTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private async Task EditProviderAsync(Guid providerId)
    {
        providerModel = await ProviderRuntimeAdministrationService.GetProviderEditorAsync(providerId);
        SyncProviderEditorText();
    }

    private async Task RefreshProvidersAfterSharedChangeAsync()
    {
        var selectedProviderId = providerModel.Id;
        providers = await ProviderRuntimeAdministrationService.ListProvidersAsync();
        RefreshProviderTreeExpansionDefaults();
        if (selectedProviderId.HasValue &&
            providers.Any(provider => provider.Id == selectedProviderId.Value))
        {
            await EditProviderAsync(selectedProviderId.Value);
        }
    }

    private async Task SaveProviderAsync()
    {
        isBusy = true;
        try
        {
            providerModel.SuggestedModels = ParseLines(suggestedModelsText).ToList();
            providerModel.Tags = providerTagValues.ToList();
            var providerId = await ProviderRuntimeAdministrationService.SaveProviderAsync(providerModel);
            providers = await ProviderRuntimeAdministrationService.ListProvidersAsync();
            RefreshProviderTreeExpansionDefaults();
            await EditProviderAsync(providerId);
            NotificationService.Success("Provider saved", "Provider profile saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider save failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task TestProviderAsync(Guid providerId)
    {
        isBusy = true;
        try
        {
            var result = await ProviderRuntimeAdministrationService.TestProviderAsync(providerId);
            providers = await ProviderRuntimeAdministrationService.ListProvidersAsync();
            await EditProviderAsync(providerId);
            if (result.Success)
            {
                NotificationService.Success("Provider health check passed", result.Summary);
            }
            else
            {
                NotificationService.Warning("Provider health check failed", result.Summary);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider health check failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task DeleteProviderAsync(Guid providerId)
    {
        isBusy = true;
        try
        {
            await ProviderRuntimeAdministrationService.DeleteProviderAsync(providerId);
            providers = await ProviderRuntimeAdministrationService.ListProvidersAsync();
            RefreshProviderTreeExpansionDefaults();
            if (providers.Count > 0)
            {
                await EditProviderAsync(providers[0].Id);
            }
            else
            {
                await ResetProviderAsync();
            }

            NotificationService.Success("Provider deleted", "Provider profile deleted.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider delete failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private Task ResetProviderAsync()
    {
        providerModel = CreateNewProviderEditor();
        providerEditorTabIndex = 0;
        SyncProviderEditorText();
        return Task.CompletedTask;
    }

    private async Task RefreshProviderModelPricesAsync()
    {
        if (!providerModel.Id.HasValue)
        {
            NotificationService.Warning(
                "Provider pricing was not loaded",
                "Save the provider before loading prices from its API.");
            return;
        }

        isBusy = true;
        try
        {
        var managedProviders = await ProviderAdministrationService.ListProviderProfilesAsync();
        if (!managedProviders.Any(provider => provider.Id == providerModel.Id.Value))
            {
                NotificationService.Warning(
                    "Provider pricing was not loaded",
                "Pricing refresh is available only for saved provider profiles. Add model prices manually.");
                return;
            }

        var administrationModel = await ProviderAdministrationService.GetProviderAsync(providerModel.Id.Value);
        if (administrationModel.Id != providerModel.Id)
            {
                NotificationService.Warning(
                    "Provider pricing was not loaded",
                "The selected provider is not a saved provider profile. Add model prices manually.");
                return;
            }

        administrationModel.Configuration.SetText(ProviderConnectorFieldKeys.BaseUrl, providerModel.BaseUrl);
        administrationModel.Configuration.SetText(ProviderConnectorFieldKeys.DefaultModel, providerModel.DefaultModel);
        administrationModel.IsPrivateProvider = providerModel.IsPrivateProvider;
        administrationModel.ModelPrices = CloneModelPrices(providerModel.ModelPrices);

        var result = await ProviderAdministrationService.RefreshProviderModelPricesAsync(administrationModel);
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
        finally
        {
            isBusy = false;
        }
    }

    private Task HandleProviderTagsChangedAsync(IReadOnlyList<string> value)
    {
        providerTagValues = value;
        providerModel.Tags = value.ToList();
        return Task.CompletedTask;
    }

    private void ResetProviderSearch()
    {
        providerSearch = string.Empty;
    }

    private void RefreshProviderTreeExpansionDefaults()
    {
        var validTagNodeIds = ProviderProfileTreeNodeBuilder.BuildTagNodeIds(FilteredProviders).ToHashSet(StringComparer.OrdinalIgnoreCase);
        expandedProviderTreeNodeIds.RemoveWhere(nodeId => !validTagNodeIds.Contains(nodeId));
        knownProviderTagNodeIds.RemoveWhere(nodeId => !validTagNodeIds.Contains(nodeId));
        foreach (var tagNodeId in validTagNodeIds)
        {
            if (knownProviderTagNodeIds.Add(tagNodeId))
            {
                expandedProviderTreeNodeIds.Add(tagNodeId);
            }
        }
    }

    private bool MatchesProviderSearch(ProviderProfile provider)
    {
        if (string.IsNullOrWhiteSpace(providerSearch))
        {
            return true;
        }

        return provider.Name.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.Kind.ToString().Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.Transport.ToString().Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.DefaultModel.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.BaseUrl.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.Tags.Any(tag => tag.Contains(providerSearch, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncProviderEditorText()
    {
        providerTagValues = providerModel.Tags
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        suggestedModelsText = string.Join(Environment.NewLine, providerModel.SuggestedModels);
    }

    private string ResolveSelectedProviderStatus()
    {
        if (!providerModel.Id.HasValue)
        {
            return "Draft provider profile.";
        }

        var provider = providers.FirstOrDefault(item => item.Id == providerModel.Id.Value);
        if (provider is null)
        {
            return "Provider is not loaded in the current catalog snapshot.";
        }

        return ProviderProfileDisplayAdapter.BuildStatusText(provider);
    }

    private static IReadOnlyList<string> ParseLines(string value)
    {
        return value
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ProviderModelTokenPriceEditorModel> CloneModelPrices(
        IEnumerable<ProviderModelTokenPriceEditorModel> prices)
    {
        return prices
            .Select(price => new ProviderModelTokenPriceEditorModel
            {
                Model = price.Model,
                InputPerMillionTokensUsd = price.InputPerMillionTokensUsd,
                CachedInputPerMillionTokensUsd = price.CachedInputPerMillionTokensUsd,
                OutputPerMillionTokensUsd = price.OutputPerMillionTokensUsd,
                CacheWritePerMillionTokensUsd = price.CacheWritePerMillionTokensUsd,
                LongContextThresholdTokens = price.LongContextThresholdTokens,
                LongContextInputPerMillionTokensUsd = price.LongContextInputPerMillionTokensUsd,
                LongContextCachedInputPerMillionTokensUsd = price.LongContextCachedInputPerMillionTokensUsd,
                LongContextCacheWritePerMillionTokensUsd = price.LongContextCacheWritePerMillionTokensUsd,
                LongContextOutputPerMillionTokensUsd = price.LongContextOutputPerMillionTokensUsd
            })
            .ToList();
    }

    private void NotifyPricingRefresh(ProviderPricingRefreshResult result)
    {
        if (result.ExplicitPriceCount > 0)
        {
            NotificationService.Success("Provider pricing loaded", result.Message);
            return;
        }

        NotificationService.Info("Provider models loaded", result.Message);
    }

    private static ProviderProfileEditorModel CreateNewProviderEditor()
    {
        return new ProviderProfileEditorModel
        {
            Name = "New OpenAI provider",
            Kind = ProviderKind.OpenAi,
            BaseUrl = ManagedSeedProviderFallbacks.OpenAiBaseUrl,
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            SupportsBackgroundResponses = true,
            PreferFrameworkManagedChatHistory = false,
            ConfigurationJson = "{}",
            SuggestedModels = ManagedSeedProviderFallbacks.OpenAiSuggestedModels.ToList(),
            IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(ProviderKind.OpenAi, null),
            ModelPrices = ProviderPricingDefaults.CreateDefaultEditorModels(
                ProviderKind.OpenAi,
                ManagedSeedProviderFallbacks.OpenAiDefaultModel),
            Tags = ["openai", "cloud", "chat", "responses"]
        };
    }
}
