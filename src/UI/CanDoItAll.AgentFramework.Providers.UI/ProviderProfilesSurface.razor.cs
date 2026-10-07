using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.Providers.UI;

public partial class ProviderProfilesSurface : IDisposable {
    [Parameter, EditorRequired] public IProviderProfilesView View { get; set; } = default!;
    [Parameter] public RenderFragment<ProviderHostSlotContext>? SharingSlot { get; set; }
    [Parameter] public RenderFragment<ProviderHostSlotContext>? HistorySlot { get; set; }
    [Parameter] public RenderFragment<ProviderHostSlotContext>? ConnectionsSlot { get; set; }
    [Parameter] public RenderFragment<ProviderHostSlotContext>? SourceRefreshSlot { get; set; }

    private readonly HashSet<string> expandedProviderTreeNodeIds = [];
    private readonly HashSet<string> knownProviderTagNodeIds = [];
    private string providerSearch = string.Empty;
    private bool disposed;
    private IReadOnlyList<ProviderProfile> providers => View.Providers;
    private IReadOnlyList<ProviderSecretChoice> secrets => View.Secrets;
    private ProviderProfileEditorModel providerModel => View.Editor.Model;
    private EditContext providerEditContext => View.Editor.Context;
    private IReadOnlyList<string> providerTagValues => providerModel.Tags;
    private bool isLoading => View.CatalogLoadState == ProviderProfilesLoadState.Loading;
    private bool isBusy => View.IsBusy;
    private bool SelectedProviderIsSourceManaged => View.SourceManaged;
    private ProviderProfile? SelectedProvider => View.SelectedProvider;
    private bool sharedConnectionsOpen {
        get => View.State.SharedConnectionsOpen;
        set => View.SetSharedConnectionsOpen(value);
    }
    private int providerEditorTabIndex {
        get => ProviderEditorSections.IndexOf(View.State.Section);
        set => View.SelectSection(ProviderEditorSections.At(value).Section);
    }
    private string suggestedModelsText => View.SourceManaged && SelectedProvider is { } provider
            ? string.Join(Environment.NewLine, providerModel.SuggestedModels.Select(provider.GetModelDisplayName))
            : View.Editor.SuggestedModelsText;
    private string ProviderDefaultModelText => View.SourceManaged && SelectedProvider is { } provider
            ? provider.GetModelDisplayName(providerModel.DefaultModel) : providerModel.DefaultModel;
    private bool HasUnlistedSecretReference => !string.IsNullOrWhiteSpace(providerModel.ApiKeyEnvironmentVariable) &&
        !secrets.Any(secret => string.Equals(secret.Reference, providerModel.ApiKeyEnvironmentVariable, StringComparison.OrdinalIgnoreCase));
    private IReadOnlyList<ProviderProfile> FilteredProviders => providers.Where(MatchesProviderSearch)
        .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    private IReadOnlyList<TreeViewNode> ProviderTreeNodes => ProviderProfileTreeNodeBuilder.Build(
        FilteredProviders, View.State.ProviderId, expandedProviderTreeNodeIds);
    private IReadOnlyList<string> AvailableProviderTags => providers.SelectMany(provider => provider.Tags)
        .Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    protected override void OnParametersSet() => RefreshProviderTreeExpansionDefaults();
    private Task LoadAsync() => View.RefreshAsync();
    private bool Current(ProviderEditorTarget target) => !disposed && target.Activation == View.Activation && ReferenceEquals(target.Context, View.Editor.Context);
    private Task ResetProviderAsync(ProviderEditorTarget target) => Current(target) ? View.NewAsync() : Task.CompletedTask;
    private void SetField<T>(ProviderEditorTarget target, ProviderEditorDraft editor, string property, T value, Action<T> assign) {
        if (Current(target) && View.CanEdit && !View.SourceManaged) {
            assign(value);
            editor.Notify(property);
        }
    }
    private void ChangeProviderKind(ProviderEditorTarget target, ProviderKind kind) {
        if (Current(target) && View.CanEdit && !View.SourceManaged) {
            View.Editor.ChangeKind(kind);
        }
    }
    private Task HandleProviderTreeSelectAsync(string nodeId) =>
        ProviderProfileTreeNodeBuilder.TryReadProviderId(nodeId, out var id) && providers.Any(provider => provider.Id == id)
            ? View.SelectAsync(id) : Task.CompletedTask;
    private void HandleProviderTreeToggleAsync(string nodeId) {
        if (!expandedProviderTreeNodeIds.Add(nodeId)) {
            expandedProviderTreeNodeIds.Remove(nodeId);
        }
    }
    private void ChangeProviderTags(ProviderEditorTarget target, IReadOnlyList<string> value) {
        if (Current(target) && View.CanEdit && !View.SourceManaged) {
            providerModel.Tags = value.ToList();
            View.Editor.Notify(nameof(providerModel.Tags));
        }
    }
    private void ChangeSuggestedModels(ProviderEditorTarget target, string? value) {
        if (Current(target) && View.CanEdit && !View.SourceManaged) {
            View.Editor.SuggestedModelsText = value ?? string.Empty;
        }
    }
    public void Dispose() => disposed = true;
    private void ResetProviderSearch() => providerSearch = string.Empty;
    private void RefreshProviderTreeExpansionDefaults() {
        var valid = ProviderProfileTreeNodeBuilder.BuildTagNodeIds(FilteredProviders).ToHashSet(StringComparer.OrdinalIgnoreCase);
        expandedProviderTreeNodeIds.RemoveWhere(id => !valid.Contains(id));
        knownProviderTagNodeIds.RemoveWhere(id => !valid.Contains(id));
        foreach (var id in valid) {
            if (knownProviderTagNodeIds.Add(id)) {
                expandedProviderTreeNodeIds.Add(id);
            }
        }
    }
    private bool MatchesProviderSearch(ProviderProfile provider) => string.IsNullOrWhiteSpace(providerSearch) ||
        provider.Name.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
        provider.Kind.ToString().Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
        provider.Transport.ToString().Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
        provider.GetModelDisplayName(provider.DefaultModel).Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
        provider.BaseUrl.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
        provider.Tags.Any(tag => tag.Contains(providerSearch, StringComparison.OrdinalIgnoreCase));
    private string ResolveSelectedProviderStatus() => !View.State.ProviderId.HasValue ? "Draft provider profile."
        : SelectedProvider is { } provider ? ProviderProfilePresentation.BuildStatusText(provider)
        : "Provider is not loaded in the current catalog snapshot.";
}
