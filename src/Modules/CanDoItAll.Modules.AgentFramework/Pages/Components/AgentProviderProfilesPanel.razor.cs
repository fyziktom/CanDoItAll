using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

using IProviderAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderAdministrationService;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using ProviderConnectorFieldKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorFieldKeys;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;
using ProviderMetadata = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMetadata;
using ProviderPricingRefreshResult = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderModelPricingRefreshResult;

public partial class AgentProviderProfilesPanel : IDisposable {
    [Inject]
    public IProviderRuntimeAdministrationService ProviderRuntimeAdministrationService { get; set; } = default!;

    [Inject]
    public IProviderAdministrationService ProviderAdministrationService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private readonly HashSet<string> expandedProviderTreeNodeIds = [];
    private readonly HashSet<string> knownProviderTagNodeIds = [];
    [Inject]
    public IProviderProfilesReads Reads { get; set; } = default!;

    private ProviderProfilesSession session = default!;
    private IReadOnlyList<ProviderProfile> providers => session.Catalog.Providers;
    private IReadOnlyList<SecretListItem> secrets => session.Catalog.Secrets.Items;
    private ProviderProfileEditorModel providerModel => session.Draft;
    private EditContext providerEditContext => session.EditContext;
    private IReadOnlyList<string> providerTagValues = [];
    private string providerSearch = string.Empty;
    private string suggestedModelsText = string.Empty;
    private int providerEditorTabIndex {
        get => ProviderEditorSections.IndexOf(session.State.Section);
        set => session.SelectSection(ProviderEditorSections.At(value).Section);
    }
    private bool isLoading => session.CatalogLoadState == ProviderProfilesLoadState.Loading;
    private bool isBusy;
    private bool sharedConnectionsOpen {
        get => session.State.SharedConnectionsOpen;
        set => session.SetSharedConnectionsOpen(value);
    }

    private IReadOnlyList<ProviderProfile> FilteredProviders => providers
        .Where(MatchesProviderSearch)
        .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<TreeViewNode> ProviderTreeNodes
        => ProviderProfileTreeNodeBuilder.Build(FilteredProviders, session.State.ProviderId, expandedProviderTreeNodeIds);

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

    private bool SelectedProviderIsSourceManaged => session.IsSourceManaged;
    private ProviderProfile? SelectedProvider => session.SelectedProvider;

    private string ProviderDefaultModelText {
        get => SelectedProviderIsSourceManaged
            ? SelectedProvider!.GetModelDisplayName(providerModel.DefaultModel)
            : providerModel.DefaultModel;
        set => providerModel.DefaultModel = value;
    }

    public void Dispose() => session?.Dispose();

    protected override async Task OnInitializedAsync() {
        session = new(Reads);
        await LoadAsync();
    }

    private async Task LoadAsync() {
        var applied = await session.RefreshAsync();
        if (session.CanEdit) {
            RefreshProviderTreeExpansionDefaults();
            if (applied) {
                SyncProviderEditorText();
            }
        }
    }

    private async Task<bool> RefreshCatalogAsync() {
        if (!await session.RefreshCatalogAsync()) {
            return false;
        }
        RefreshProviderTreeExpansionDefaults();
        return true;
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

    private async Task<bool> EditProviderAsync(Guid providerId) {
        if (!await session.SelectAsync(providerId)) {
            return false;
        }
        RefreshProviderTreeExpansionDefaults();
        SyncProviderEditorText();
        return true;
    }

    private Task RefreshProvidersAfterSharedChangeAsync() => LoadAsync();

    private async Task SaveProviderAsync()
    {
        if (!session.CanEdit || isBusy) {
            return;
        }
        var version = session.SelectionVersion;
        isBusy = true;
        try
        {
            providerModel.SuggestedModels = ParseLines(suggestedModelsText).ToList();
            if (string.IsNullOrWhiteSpace(providerModel.DefaultModel) ||
                (providerModel.SuggestedModels.Count > 0 &&
                 !providerModel.SuggestedModels.Contains(providerModel.DefaultModel.Trim(), StringComparer.OrdinalIgnoreCase))) {
                throw new ProviderProfileValidationException("Choose a default model from this provider's model catalog before saving.");
            }
            providerModel.Tags = providerTagValues.ToList();
            var providerId = await ProviderRuntimeAdministrationService.SaveProviderAsync(providerModel);
            if (!session.IsCurrentSelection(version) || !await RefreshCatalogAsync()
                || !session.IsCurrentSelection(version)) {
                return;
            }
            if (!await EditProviderAsync(providerId)) {
                return;
            }
            NotificationService.Success("Provider saved", "Provider profile saved.");
        }
        catch (Exception exception) when (session.IsCurrentSelection(version))
        {
            NotificationService.Error("Provider save failed", exception.Message);
        }
        catch (Exception) when (!session.IsCurrentSelection(version)) {
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task TestProviderAsync(Guid providerId)
    {
        if (!session.CanEdit || isBusy) {
            return;
        }
        var version = session.SelectionVersion;
        isBusy = true;
        try
        {
            var result = await ProviderRuntimeAdministrationService.TestProviderAsync(providerId);
            if (!session.IsCurrentSelection(version) || !await RefreshCatalogAsync()
                || !session.IsCurrentSelection(version)) {
                return;
            }
            if (!await EditProviderAsync(providerId)) {
                return;
            }
            if (result.Success)
            {
                NotificationService.Success("Provider health check passed", result.Summary);
            }
            else
            {
                NotificationService.Warning("Provider health check failed", result.Summary);
            }
        }
        catch (Exception exception) when (session.IsCurrentSelection(version))
        {
            NotificationService.Error("Provider health check failed", exception.Message);
        }
        catch (Exception) when (!session.IsCurrentSelection(version)) {
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task DeleteProviderAsync(Guid providerId)
    {
        if (!session.CanEdit || isBusy) {
            return;
        }
        var version = session.SelectionVersion;
        isBusy = true;
        try
        {
            await ProviderRuntimeAdministrationService.DeleteProviderAsync(providerId);
            if (!session.IsCurrentSelection(version) || !await RefreshCatalogAsync()
                || !session.IsCurrentSelection(version)) {
                return;
            }
            if (providers.Count > 0)
            {
                if (!await EditProviderAsync(providers[0].Id)) {
                    return;
                }
            }
            else
            {
                await ResetProviderAsync();
            }

            NotificationService.Success("Provider deleted", "Provider profile deleted.");
        }
        catch (Exception exception) when (session.IsCurrentSelection(version))
        {
            NotificationService.Error("Provider delete failed", exception.Message);
        }
        catch (Exception) when (!session.IsCurrentSelection(version)) {
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task ResetProviderAsync() {
        await session.NewAsync();
        SyncProviderEditorText();
    }

    private void ChangeProviderKind(ProviderKind kind) {
        if (SelectedProviderIsSourceManaged || providerModel.Kind == kind) {
            return;
        }

        providerModel.Kind = kind;
        providerModel.BaseUrl = string.Empty;
        providerModel.ApiKeyEnvironmentVariable = string.Empty;
        providerModel.DefaultModel = string.Empty;
        providerModel.ConfigurationJson = "{}";
        providerModel.SuggestedModels = [];
        providerModel.ModelPrices = [];
        providerModel.ModelThinkingEffortCapabilities = [];
        providerModel.Tags = [];
        providerModel.IsPrivateProvider = ProviderPricingDefaults.IsPrivateProvider(kind);
        providerModel.Transport = kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi
            ? ProviderTransportKind.Responses : ProviderTransportKind.ChatCompletions;
        providerModel.SupportsBackgroundResponses = false;
        SyncProviderEditorText();
        NotificationService.Info("Provider kind changed",
            "Connection, models, prices and provider metadata were cleared. Configure the endpoint and load its models before saving.");
    }

    private async Task RefreshProviderModelPricesAsync() {
        if (!session.CanEdit || isBusy || SelectedProviderIsSourceManaged) {
            return;
        }
        var version = session.SelectionVersion;
        isBusy = true;
        try {
            var administrationModel = new ProviderManagement.ProviderProfileEditorModel {
                Id = providerModel.Id,
                Name = providerModel.Name,
                ConnectorPluginKey = ProviderMetadata.ResolveConnectorPluginKey(providerModel, null),
                ApiKeySecretId = ProviderMetadata.ResolveSecretRecordId(providerModel),
                Configuration = ConnectorConfigState.FromJson(providerModel.ConfigurationJson),
                IsPrivateProvider = providerModel.IsPrivateProvider,
                ModelPrices = CloneModelPrices(providerModel.ModelPrices)
            };
            administrationModel.Configuration.SetText(ProviderConnectorFieldKeys.BaseUrl, providerModel.BaseUrl);
            administrationModel.Configuration.SetText(ProviderConnectorFieldKeys.DefaultModel, providerModel.DefaultModel);
            var result = await ProviderAdministrationService.RefreshProviderModelPricesAsync(administrationModel);
            if (!session.IsCurrentSelection(version)) {
                return;
            }
            if (!result.IsSuccess)
            {
                NotificationService.Warning(
                    "Provider pricing was not loaded",
                    string.Join(" ", result.Errors.Select(error => error.Message)));
                return;
            }

            providerModel.ModelPrices = result.Value!.ModelPrices;
            providerModel.SuggestedModels = result.Value.Models.ToList();
            SyncProviderEditorText();
            if (!providerModel.SuggestedModels.Contains(providerModel.DefaultModel, StringComparer.OrdinalIgnoreCase)) {
                providerModel.DefaultModel = string.Empty;
                NotificationService.Warning("Provider models loaded",
                    $"{result.Value.Message} Select a default model from the loaded catalog before saving.");
            } else {
                NotifyPricingRefresh(result.Value);
            }
        }
        catch (Exception exception) when (session.IsCurrentSelection(version))
        {
            NotificationService.Error("Provider pricing load failed", exception.Message);
        }
        catch (Exception) when (!session.IsCurrentSelection(version)) {
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
               provider.GetModelDisplayName(provider.DefaultModel).Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.BaseUrl.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.Tags.Any(tag => tag.Contains(providerSearch, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncProviderEditorText() {
        providerTagValues = providerModel.Tags
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        suggestedModelsText = string.Join(Environment.NewLine, SelectedProviderIsSourceManaged
            ? providerModel.SuggestedModels.Select(SelectedProvider!.GetModelDisplayName)
            : providerModel.SuggestedModels);
    }

    private string ResolveSelectedProviderStatus()
    {
        if (!session.State.ProviderId.HasValue)
        {
            return "Draft provider profile.";
        }

        var provider = SelectedProvider;
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

}
