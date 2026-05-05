using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCatalogPanel
{
    [Parameter]
    public Guid? RequestedAgentId { get; set; }

    [Parameter]
    public IReadOnlyList<AgentDefinition>? InitialAgents { get; set; }

    [Parameter]
    public IReadOnlyList<ProviderProfile>? InitialProviders { get; set; }

    [Parameter]
    public bool SkipCatalogRepair { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentFrameworkOrganizationCatalogRepairService OrganizationCatalogRepairService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private string agentSearch = string.Empty;
    private bool hasLoaded;
    private bool isLoading = true;
    private bool interactiveReloadAttempted;
    private Task? loadTask;
    private Guid? selectedAgentId;
    private Guid? openedRequestedAgentId;

    private IReadOnlyList<AgentDefinition> FilteredAgents => agents
        .Where(agent =>
            string.IsNullOrWhiteSpace(agentSearch) ||
            agent.Name.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.RoleTitle.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.Summary.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.Model.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.Tags.Any(tag => tag.Contains(agentSearch, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await EnsureLoadedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await EnsureLoadedAsync();
        OpenRequestedAgentDialogIfNeeded();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender ||
            interactiveReloadAttempted ||
            hasLoaded)
        {
            return;
        }

        interactiveReloadAttempted = true;
        await EnsureLoadedAsync();
        StateHasChanged();
    }

    private Task EnsureLoadedAsync()
    {
        if (hasLoaded)
        {
            return Task.CompletedTask;
        }

        if (loadTask is not null)
        {
            return loadTask;
        }

        loadTask = LoadAsync();
        return loadTask;
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            if (!SkipCatalogRepair)
            {
                await OrganizationCatalogRepairService.EnsureCurrentOrganizationCatalogAsync();
            }

            agents = InitialAgents is null
                ? (await WorkspaceService.ListAgentsAsync(includeTemplates: false)).ToList()
                : InitialAgents.ToList();

            if (RequestedAgentId.HasValue &&
                agents.Any(item => item.Id == RequestedAgentId.Value))
            {
                selectedAgentId = RequestedAgentId.Value;
            }

            hasLoaded = true;
        }
        finally
        {
            isLoading = false;
            loadTask = null;
        }
    }

    private void OpenRequestedAgentDialogIfNeeded()
    {
        if (!RequestedAgentId.HasValue ||
            openedRequestedAgentId == RequestedAgentId.Value ||
            !agents.Any(item => item.Id == RequestedAgentId.Value))
        {
            return;
        }

        openedRequestedAgentId = RequestedAgentId.Value;
        selectedAgentId = RequestedAgentId.Value;
        _ = OpenAgentDetailsDialogAsync(RequestedAgentId.Value);
    }

    private void SelectAgent(Guid agentId)
    {
        selectedAgentId = agentId;
    }

    private Task OpenNewAgentDialogAsync()
        => QueueAgentDetailsDialogAsync(agentId: null);

    private Task QueueAgentDetailsDialogAsync(Guid? agentId)
    {
        _ = OpenAgentDetailsDialogAsync(agentId);
        return Task.CompletedTask;
    }

    private async Task OpenAgentDetailsDialogAsync(Guid? agentId)
    {
        if (agentId.HasValue)
        {
            selectedAgentId = agentId.Value;
        }

        try
        {
            var title = agentId.HasValue
                ? ResolveDialogTitle(agentId.Value)
                : "New technical agent";

            var result = await DialogService.OpenAsync<AgentDetailsDialog>(
                title,
                new Dictionary<string, object?>
                {
                    [nameof(AgentDetailsDialog.AgentId)] = agentId,
                    [nameof(AgentDetailsDialog.InitialProviders)] = InitialProviders,
                    [nameof(AgentDetailsDialog.Saved)] =
                        EventCallback.Factory.Create<AgentDetailsDialogResult>(this, HandleAgentDialogSavedAsync)
                },
                new DialogOptions
                {
                    Eyebrow = "Technical editor",
                    Subtitle = "Edit identity, runtime, access policy, skills, and MCP servers for this technical agent.",
                    Size = ModalSize.Full,
                    DenseChrome = true,
                    AriaLabel = "Agent details editor",
                    TestId = "agents-details-dialog"
                });

            if (result is AgentDetailsDialogResult dialogResult)
            {
                await HandleAgentDialogSavedAsync(dialogResult);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent dialog failed", exception.Message);
        }
    }

    private string ResolveDialogTitle(Guid agentId)
    {
        var agent = agents.FirstOrDefault(item => item.Id == agentId);
        return string.IsNullOrWhiteSpace(agent?.Name)
            ? "Agent details"
            : agent.Name;
    }

    private async Task HandleAgentDialogSavedAsync(AgentDetailsDialogResult result)
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        if (result.Deleted)
        {
            selectedAgentId = agents.FirstOrDefault()?.Id;
            return;
        }

        if (result.AgentId.HasValue)
        {
            selectedAgentId = result.AgentId.Value;
        }

        await InvokeAsync(StateHasChanged);
    }

    private void ResetAgentSearch()
    {
        agentSearch = string.Empty;
    }
}
