using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using Microsoft.AspNetCore.Components;

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
    public AiAgentService AiAgentService { get; set; } = default!;

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
        new("agents", "Agents", technicalAgentCount.ToString()),
        new("providers", "Providers", providerCount.ToString()),
        new("chat", "Chat"),
        new("capabilities", "Capabilities", capabilityCount.ToString()),
        new("governance", "Governance", activeRunCount.ToString()),
        new("scenarios", "Scenarios"),
        new("diagnostics", "Diagnostics", failedRunCount.ToString())
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
        dashboard = await WorkspaceService.GetDashboardAsync();
        technicalAgentCount = dashboard.AgentCount;
        providerCount = dashboard.ProviderCount;
        capabilityCount = dashboard.CapabilityCount;
        activeRunCount = dashboard.ActiveRuns;
        failedRunCount = dashboard.FailedRuns;
        boundResourceCount = (await AiAgentService.ListAgentDirectoryAsync())
            .Count(item => item.TechnicalAgentId.HasValue && item.BindingStatus == AiResourceBindingStatus.Bound);
        ApplyRequestedTab();
    }

    private Task HandleTabChangedAsync(
        string key)
    {
        selectedTab = key;
        Navigation.NavigateTo(BuildAgentsRoute(key, RequestedAgentId), replace: true);
        return Task.CompletedTask;
    }

    private void ApplyRequestedTab()
    {
        selectedTab = !string.IsNullOrWhiteSpace(RequestedTab) &&
                      AllowedTabs.Contains(RequestedTab)
            ? RequestedTab
            : "overview";
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

    private void OpenCrmHrAgents()
    {
        Navigation.NavigateTo("/crm-hr/agents");
    }

    private void OpenProcesses()
    {
        Navigation.NavigateTo("/processes");
    }
}
