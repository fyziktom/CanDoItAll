using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class AgentsHomePage
{
    private static readonly HashSet<string> AllowedTabs =
    [
        "overview",
        "agents",
        "providers",
        "chat",
        "capabilities",
        "governance",
        "scenarios",
        "diagnostics"
    ];

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentFrameworkOrganizationCatalogRepairService OrganizationCatalogRepairService { get; set; } = default!;

    [Inject]
    public IDbContextFactory<AppDbContext> DbContextFactory { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [SupplyParameterFromQuery(Name = "agentId")]
    public Guid? RequestedAgentId { get; set; }

    private int technicalAgentCount;
    private int providerCount;
    private int boundResourceCount;
    private int capabilityCount;
    private int activeRunCount;
    private int failedRunCount;
    private string selectedTab = "overview";
    private Guid? effectiveRequestedAgentId;
    private bool isLoaded;
    private SandboxDashboardSnapshot dashboard = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        ExecutionBoundaryDescriptor.Unknown);

    private IReadOnlyList<SecondaryTabItem> Tabs =>
    [
        new("overview", "Overview"),
        new("agents", "Agents", ResolveSummaryValue(technicalAgentCount)),
        new("providers", "Providers", ResolveSummaryValue(providerCount)),
        new("chat", "Chat"),
        new("capabilities", "Capabilities", ResolveSummaryValue(capabilityCount)),
        new("governance", "Governance", ResolveSummaryValue(activeRunCount)),
        new("scenarios", "Scenarios"),
        new("diagnostics", "Diagnostics", ResolveSummaryValue(failedRunCount))
    ];

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
        await OrganizationCatalogRepairService.EnsureCurrentOrganizationCatalogAsync();

        var dashboardTask = WorkspaceService.GetDashboardAsync();
        var agentsTask = WorkspaceService.ListAgentsAsync(includeTemplates: false);
        var providersTask = WorkspaceService.ListProvidersAsync();
        var capabilitiesTask = WorkspaceService.ListCapabilitiesAsync();
        await Task.WhenAll(dashboardTask, agentsTask, providersTask, capabilitiesTask);

        dashboard = await dashboardTask;
        var currentAgents = (await agentsTask).ToList();
        technicalAgentCount = currentAgents.Count;
        providerCount = (await providersTask).Count;
        capabilityCount = (await capabilitiesTask).Count;
        activeRunCount = dashboard.ActiveRuns;
        failedRunCount = dashboard.FailedRuns;
        if (currentAgents.Count == 0)
        {
            boundResourceCount = 0;
        }
        else
        {
            var technicalAgentIds = currentAgents
                .Select(item => item.Id)
                .ToList();
            await using var dbContext = await DbContextFactory.CreateDbContextAsync();
            boundResourceCount = await dbContext.Set<AiResourceBinding>()
                .CountAsync(
                    item => item.TechnicalAgentId.HasValue &&
                            technicalAgentIds.Contains(item.TechnicalAgentId.Value) &&
                            item.BindingStatus == AiResourceBindingStatus.Bound);
        }

        isLoaded = true;
        ApplyRequestedTab();
    }

    private Task HandleTabChangedAsync(
        string key)
    {
        selectedTab = key;
        Navigation.NavigateTo(BuildAgentsRoute(key, effectiveRequestedAgentId), replace: true);
        return Task.CompletedTask;
    }

    private void ApplyRequestedTab()
    {
        var requestedTab = ResolveRequestedTab();
        effectiveRequestedAgentId = ResolveRequestedAgentId();
        selectedTab = !string.IsNullOrWhiteSpace(requestedTab) &&
                      AllowedTabs.Contains(requestedTab)
            ? requestedTab
            : "overview";
    }

    private string? ResolveRequestedTab()
    {
        if (!string.IsNullOrWhiteSpace(RequestedTab))
        {
            return RequestedTab;
        }

        return TryGetQueryValue("tab");
    }

    private Guid? ResolveRequestedAgentId()
    {
        if (RequestedAgentId.HasValue)
        {
            return RequestedAgentId;
        }

        return Guid.TryParse(TryGetQueryValue("agentId"), out var agentId)
            ? agentId
            : null;
    }

    private string? TryGetQueryValue(
        string key)
    {
        var query = Navigation.ToAbsoluteUri(Navigation.Uri).Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = pair.Split('=', 2);
            if (!string.Equals(Uri.UnescapeDataString(segments[0]), key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return segments.Length > 1
                ? Uri.UnescapeDataString(segments[1])
                : string.Empty;
        }

        return null;
    }

    private static string BuildAgentsRoute(
        string tab,
        Guid? agentId)
    {
        if (!agentId.HasValue &&
            string.Equals(tab, "overview", StringComparison.Ordinal))
        {
            return "/agents";
        }

        if (agentId.HasValue)
        {
            return $"/agents?tab={Uri.EscapeDataString(tab)}&agentId={agentId.Value:D}";
        }

        return $"/agents?tab={Uri.EscapeDataString(tab)}";
    }

    private string ResolveSummaryValue(int value)
    {
        return isLoaded ? value.ToString() : "...";
    }

    private void OpenCrmHrAgents()
    {
        Navigation.NavigateTo("/crm-hr/agents");
    }

    private void OpenProcesses()
    {
        Navigation.NavigateTo("/processes");
    }
}
