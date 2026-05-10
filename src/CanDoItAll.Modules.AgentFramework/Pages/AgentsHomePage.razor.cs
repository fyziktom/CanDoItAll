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
    private const string AgentFrameworkShellHelpText =
        "This shell owns the technical agent catalog, durable execution evidence, provider diagnostics, and scenario proof. CRM-HR consumes that catalog through its business-facing directory and bridge surfaces, while Processes and Collaboration stay canonical for launch, run, and approval governance.";

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
    private AgentFrameworkCatalogWarmupService CatalogWarmupService { get; set; } = default!;

    [Inject]
    public IDbContextFactory<AppDbContext> DbContextFactory { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

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
    private bool isFeedingDefaults;
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

    private bool ShouldShowOverviewLoadingCard =>
        !isLoaded &&
        string.Equals(selectedTab, "overview", StringComparison.Ordinal);

    protected override Task OnInitializedAsync()
    {
        return Task.CompletedTask;
    }

    protected override void OnParametersSet()
    {
        ApplyRequestedTab();
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return Task.CompletedTask;
        }

        _ = InitializeShellAsync();
        return Task.CompletedTask;
    }

    private async Task InitializeShellAsync()
    {
        await RefreshShellAsync();
    }

    private async Task RefreshShellAsync()
    {
        try
        {
            await LoadDashboardAsync();
        }
        catch (Exception exception)
        {
            SetStatusError($"Failed to load agent runtime summary. {exception.Message}");
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadDashboardAsync()
    {
        var dashboardTask = WorkspaceService.GetDashboardAsync();
        var boundResourceCountTask = LoadBoundResourceCountAsync();
        await Task.WhenAll(dashboardTask, boundResourceCountTask);

        dashboard = await dashboardTask;
        technicalAgentCount = dashboard.AgentCount;
        providerCount = dashboard.ProviderCount;
        capabilityCount = dashboard.CapabilityCount;
        activeRunCount = dashboard.ActiveRuns;
        failedRunCount = dashboard.FailedRuns;
        boundResourceCount = await boundResourceCountTask;

        isLoaded = true;
        ApplyRequestedTab();
    }

    private async Task<int> LoadBoundResourceCountAsync()
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<AiResourceBinding>()
            .CountAsync(item =>
                item.TechnicalAgentId.HasValue &&
                item.BindingStatus == AiResourceBindingStatus.Bound);
    }

    private async Task FeedDefaultsAsync()
    {
        if (isFeedingDefaults)
        {
            return;
        }

        isFeedingDefaults = true;
        ClearStatusMessage();

        try
        {
            await CatalogWarmupService.WarmupAsync();
            await LoadDashboardAsync();
            SetStatusMessage("Default agents, capabilities, and CRM-HR projection were synchronized.");
        }
        catch (Exception exception)
        {
            SetStatusError($"Failed to synchronize default agents. {exception.Message}");
        }
        finally
        {
            isFeedingDefaults = false;
            await InvokeAsync(StateHasChanged);
        }
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

    private void SetStatusMessage(string value)
    {
        NotificationService.Success("AgentFramework updated", value);
    }

    private void SetStatusError(string value)
    {
        NotificationService.Error("AgentFramework update failed", value);
    }

    private void ClearStatusMessage()
    {
    }

    private void OpenCrmHrAgents()
    {
        Navigation.NavigateTo("/crm-hr/agents");
    }

    private void OpenProcesses()
    {
        Navigation.NavigateTo("/processes");
    }

    private void OpenWorkflows()
    {
        Navigation.NavigateTo("/agents/workflows");
    }
}
