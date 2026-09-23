using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;
using ProviderMetadata = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMetadata;

public partial class AgentProviderProfilesPanel : IDisposable {
    [Inject]
    public IProviderEditorCommands Commands { get; set; } = default!;

    [Inject] public ProviderEditorRecovery Recovery { get; set; } = default!;

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
    private string rawSuggestedModels = string.Empty;
    private string suggestedModelsText {
        get => rawSuggestedModels;
        set {
            rawSuggestedModels = value;
            if (!SelectedProviderIsSourceManaged) {
                providerModel.SuggestedModels = ParseLines(value).ToList();
            }
        }
    }
    private int providerEditorTabIndex {
        get => ProviderEditorSections.IndexOf(session.State.Section);
        set => session.SelectSection(ProviderEditorSections.At(value).Section);
    }
    private bool isLoading => session.CatalogLoadState == ProviderProfilesLoadState.Loading;
    private ProviderEditorOperations operations = default!;
    private bool isBusy => operations.IsBusy;
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
        session = new(Reads, Recovery);
        operations = new(session, Commands);
        await LoadAsync();
    }

    private async Task LoadAsync() {
        if (operations.HasPendingReconciliation) {
            await RetryReconciliationAsync();
            return;
        }
        var applied = await session.RefreshAsync();
        if (session.CanEdit) {
            RefreshProviderTreeExpansionDefaults();
            if (applied) {
                SyncProviderEditorText();
            }
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

    private async Task<bool> EditProviderAsync(Guid providerId) {
        if (!await session.SelectAsync(providerId)) {
            return false;
        }
        RefreshProviderTreeExpansionDefaults();
        SyncProviderEditorText();
        return true;
    }

    private long sharingRevision;

    private Task RefreshProvidersAfterSharedChangeAsync(SharedProviderChangeDelivery delivery) =>
        delivery.ReconcileAsync(async () => {
            var change = delivery.Change;
            var selectedId = session.State.ProviderId;
            var result = await session.ReconcileSharedAsync(change);
            if (!result.Completed) {
                throw new InvalidOperationException("The provider workspace reconciliation did not complete.");
            }
            if (result.EditorReplaced) {
                SyncProviderEditorText();
            }
            if (selectedId.HasValue && session.State.ProviderId == selectedId &&
                change.AffectedProviderProfileIds.Contains(selectedId.Value) &&
                change.Kind is not (ProviderManagement.SharedProviderChangeKind.Publication or
                    ProviderManagement.SharedProviderChangeKind.ImportedSettings or
                    ProviderManagement.SharedProviderChangeKind.ImportRetirement)) {
                sharingRevision++;
            }
            RefreshProviderTreeExpansionDefaults();
        });

    private async Task SaveProviderAsync() {
        providerModel.SuggestedModels = ParseLines(suggestedModelsText).ToList();
        providerModel.Tags = providerTagValues.ToList();
        PublishFeedback(await operations.SaveAsync());
        RefreshProviderTreeExpansionDefaults();
    }

    private async Task TestProviderAsync(Guid providerId) {
        PublishFeedback(await operations.CheckHealthAsync());
    }

    private async Task DeleteProviderAsync(Guid providerId) {
        PublishFeedback(await operations.DeleteAsync());
        RefreshProviderTreeExpansionDefaults();
    }

    private async Task RetryReconciliationAsync() {
        PublishFeedback(await operations.RetryReconciliationAsync());
        RefreshProviderTreeExpansionDefaults();
    }

    private async Task VerifyUnconfirmedAsync() {
        PublishFeedback(await operations.VerifyUnconfirmedAsync());
        RefreshProviderTreeExpansionDefaults();
    }

    private async Task RetryVerifiedAsync() {
        PublishFeedback(await operations.RetryVerifiedAsync());
        RefreshProviderTreeExpansionDefaults();
    }

    private void PublishFeedback(ProviderEditorFeedback? feedback) {
        if (feedback is null) {
            return;
        }
        switch (feedback.Kind) {
            case ProviderFeedbackKind.Success:
                NotificationService.Success(feedback.Title, feedback.Message);
                break;
            case ProviderFeedbackKind.Warning:
                NotificationService.Warning(feedback.Title, feedback.Message);
                break;
            case ProviderFeedbackKind.Error:
                NotificationService.Error(feedback.Title, feedback.Message);
                break;
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
        var feedback = await operations.DiscoverModelsAsync();
        if (feedback?.Kind == ProviderFeedbackKind.Success) {
            SyncProviderEditorText();
        }
        PublishFeedback(feedback);
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

}
