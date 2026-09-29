using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Resources.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Resources.Pages;

public partial class ResourcesPage : IAsyncDisposable {
    [SupplyParameterFromQuery(Name = "resourceId")] public Guid? ResourceIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = "projectId")] public Guid? ProjectIdQuery { get; set; }
    [Inject] public IResourceRegistryOwner RegistryOwner { get; set; } = default!;
    [Inject] public IResourceBrowseOwner BrowseOwner { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public IJSRuntime JSRuntime { get; set; } = default!;
    public ResourceRegistryController Registry { get; private set; } = default!;
    public ResourceBrowseController Browse { get; private set; } = default!;
    private int tab;
    private bool disposed;
    private AgentChatContextSurface AgentChatSurface => ResourcesAgentChatContextBuilder.Build(
        tab == 1 ? ResourcesAgentChatView.Browse : ResourcesAgentChatView.Registry, Registry.Draft.Editor,
        Registry.SelectedProjectName, Registry.SelectedManifest?.DisplayName ?? "Unavailable connector",
        Browse.Position is { } position ? new(new(position.SourceId.Value), position.SourceClass.ToString(), position.DisplayName, position.ProjectId, position.ProjectName) : null);
    private AgentChatContextAccessState CurrentAccess => (tab == 1 ? Browse.Access : Registry.Access) switch {
        ResourceViewAccess.Ready => AgentChatContextAccessState.Ready,
        ResourceViewAccess.Failed => AgentChatContextAccessState.Failed,
        _ => AgentChatContextAccessState.Loading
    };
    private AgentChatNavigationIdentity AgentChatNavigationFence => AgentChatNavigationIdentity.CreateForLocation(Navigation.BaseUri, Navigation.Uri,
        [new("resourceId", ResourceIdQuery?.ToString("D")), new("projectId", ProjectIdQuery?.ToString("D"))]);
    protected override void OnInitialized() {
        Registry = new(RegistryOwner);
        Browse = new(BrowseOwner, new(JSRuntime)) { Promoted = _ => Registry.RefreshAfterPromotionAsync() };
        Registry.Changed += OnChanged;
        Browse.Changed += OnChanged;
        Registry.Completed += NotifyOutcome;
    }
    protected override async Task OnParametersSetAsync() {
        await Registry.LoadRouteAsync(ResourceIdQuery, ProjectIdQuery);
        if (Browse.Catalog is null) {
            await Browse.RefreshAsync();
        }
    }
    private void SetTab(int selected) => tab = selected;
    private void OnChanged() {
        if (!disposed) {
            _ = InvokeAsync(StateHasChanged);
        }
    }
    private void NotifyOutcome(ResourceMutationReceipt receipt) {
        var saved = receipt.Kind == ResourceMutationKind.Save;
        var action = saved ? "saved" : "deleted";
        if (receipt.State == ResourceEffectState.Committed) {
            NotificationService.Success($"Resource {action}", receipt.Message);
        } else if (receipt.State == ResourceEffectState.CommittedWarning) {
            NotificationService.Warning($"Resource {action}; refresh incomplete", $"Resource '{receipt.ResourceId:D}' was {action}. {receipt.Message}");
        } else if (receipt.State == ResourceEffectState.Unknown) {
            NotificationService.Warning(saved ? "Resource save outcome unknown" : "Resource delete outcome unknown", receipt.Message);
        } else {
            NotificationService.Error(saved ? "Resource save failed" : "Resource delete failed", receipt.Message);
        }
    }
    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        Registry.Changed -= OnChanged;
        Browse.Changed -= OnChanged;
        Registry.Completed -= NotifyOutcome;
        Registry.Dispose();
        await Browse.DisposeAsync();
    }
}
