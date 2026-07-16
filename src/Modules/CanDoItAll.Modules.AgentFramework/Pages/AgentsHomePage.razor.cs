using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
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
        "voice",
        "floating-chat",
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

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [SupplyParameterFromQuery(Name = "agentId")]
    public Guid? RequestedAgentId { get; set; }

    [SupplyParameterFromQuery(Name = "teamId")]
    public Guid? RequestedTeamId { get; set; }

    private int technicalAgentCount;
    private int providerCount;
    private int boundResourceCount;
    private int capabilityCount;
    private int activeRunCount;
    private int failedRunCount;
    private string selectedTab = "overview";
    private Guid? effectiveRequestedAgentId;
    private Guid? effectiveRequestedTeamId;
    private bool isLoaded;
    private bool isFeedingDefaults;
    private bool hasOverviewLoadError;
    private string? overviewLoadError;
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
    private AgentOverviewSnapshot overview = AgentOverviewSnapshot.Empty;

    private static readonly CdaChartOptions ProviderUsageBarChartOptions = new()
    {
        Type = CdaChartType.Bar,
        XAxisType = CdaChartAxisType.Category,
        Unit = "observations",
        YAxisTitle = "Usage observations",
        ShowToolbar = false,
        EnableZoom = false,
        ShowLegend = false,
        ValuePrecision = 0,
        TooltipPrecision = 0,
        Palette = CdaChartPalette.Calm
    };

    private static readonly CdaChartOptions ProviderUsageDistributionChartOptions = new()
    {
        Type = CdaChartType.Donut,
        XAxisType = CdaChartAxisType.Category,
        Unit = "observations",
        ShowToolbar = false,
        EnableZoom = false,
        ShowLegend = true,
        ShowDataLabels = true,
        ValuePrecision = 0,
        TooltipPrecision = 0,
        LegendPosition = CdaChartLegendPosition.Bottom,
        Palette = CdaChartPalette.Energetic
    };

    private IReadOnlyList<SecondaryTabItem> Tabs =>
    [
        new("overview", "Overview"),
        new("agents", "Agents", ResolveSummaryValue(technicalAgentCount)),
        new("providers", "Providers", ResolveSummaryValue(providerCount)),
        new("voice", "Voice"),
        new("floating-chat", "Floating chat"),
        new("chat", "Chat"),
        new("capabilities", "Capabilities", ResolveSummaryValue(capabilityCount)),
        new("governance", "Governance", ResolveSummaryValue(activeRunCount)),
        new("scenarios", "Scenarios"),
        new("diagnostics", "Diagnostics", ResolveSummaryValue(failedRunCount))
    ];

    private IReadOnlyList<OverviewMetricBadge> OverviewMetricBadges =>
    [
        new(
            "Agents",
            ResolveOverviewValue(overview.Totals.AgentCount),
            "groups",
            "info",
            "Organization-scoped technical runtime records.",
            "agents-overview-metric-agents"),
        new(
            "Teams",
            ResolveOverviewValue(overview.Totals.TeamCount),
            "hub",
            "success",
            "Agent teams available from the technical catalog.",
            "agents-overview-metric-teams"),
        new(
            "Providers",
            ResolveOverviewValue(overview.Totals.ProviderCount),
            "cloud",
            "accent",
            "Workspace-owned providers executed through AgentFramework.",
            "agents-overview-metric-providers"),
        new(
            "Capabilities",
            ResolveOverviewValue(overview.Totals.CapabilityCount),
            "extension",
            "warning",
            "Reusable skills, MCP servers, and other runtime capabilities.",
            "agents-overview-metric-capabilities"),
        new(
            "Sessions",
            ResolveOverviewValue(overview.Totals.SessionCount),
            "forum",
            "neutral",
            "Chat and runtime sessions associated with the workspace.",
            "agents-overview-metric-sessions"),
        new(
            "Usage",
            ResolveOverviewValue(overview.Totals.UsageObservationCount),
            "monitor_heart",
            "info",
            "Usage observations captured from technical executions.",
            "agents-overview-metric-usage"),
        new(
            "Tokens",
            ResolveOverviewTokens(overview.Totals.TotalTokens),
            "token",
            "success",
            "Provider-reported total token usage.",
            "agents-overview-metric-tokens"),
        new(
            "Cost",
            ResolveOverviewCost(overview.Totals.KnownCostUsd),
            "paid",
            "danger",
            "Known provider cost from execution telemetry.",
            "agents-overview-metric-cost")
    ];

    private IReadOnlyList<ProviderOverviewUsageRow> OverviewProviderRows =>
        overview.ProviderUsage
            .OrderByDescending(AgentUsageDisplay.ResolveProviderUsageValue)
            .Take(6)
            .ToArray();

    private IReadOnlyList<CdaChartSeries> ProviderUsageBarSeries =>
        OverviewProviderRows.Count == 0
            ? []
            :
            [
                new CdaChartSeries
                {
                    Name = "Provider usage",
                    Type = CdaChartType.Bar,
                    Points = OverviewProviderRows
                        .Select(item => new CdaChartPoint(
                            AgentUsageDisplay.TrimLabel(item.ProviderName, 22),
                            AgentUsageDisplay.ResolveProviderUsageValue(item)))
                        .ToArray()
                }
            ];

    private IReadOnlyList<CdaChartSeries> ProviderUsageDistributionSeries =>
        OverviewProviderRows.Count == 0
            ? []
            :
            [
                new CdaChartSeries
                {
                    Name = "Provider share",
                    Type = CdaChartType.Donut,
                    Points = OverviewProviderRows
                        .Select(item => new CdaChartPoint(
                            AgentUsageDisplay.TrimLabel(item.ProviderName, 22),
                            AgentUsageDisplay.ResolveProviderUsageValue(item)))
                        .ToArray()
                }
            ];

    private static string ResolveOverviewMetricBadgeClass(string tone)
    {
        return tone switch
        {
            "success" => "agents-overview-stat-badge agents-overview-stat-badge--success",
            "warning" => "agents-overview-stat-badge agents-overview-stat-badge--warning",
            "danger" => "agents-overview-stat-badge agents-overview-stat-badge--danger",
            "accent" => "agents-overview-stat-badge agents-overview-stat-badge--accent",
            "neutral" => "agents-overview-stat-badge agents-overview-stat-badge--neutral",
            _ => "agents-overview-stat-badge agents-overview-stat-badge--info"
        };
    }

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
            hasOverviewLoadError = false;
            overviewLoadError = null;
            await LoadDashboardAsync();
        }
        catch (Exception exception)
        {
            hasOverviewLoadError = true;
            overviewLoadError = exception.Message;
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
        var overviewTask = WorkspaceService.GetAgentOverviewAsync();
        var boundResourceCountTask = LoadBoundResourceCountAsync();
        await Task.WhenAll(dashboardTask, overviewTask, boundResourceCountTask);

        dashboard = await dashboardTask;
        overview = await overviewTask;
        technicalAgentCount = overview.Totals.AgentCount;
        providerCount = overview.Totals.ProviderCount;
        capabilityCount = overview.Totals.CapabilityCount;
        activeRunCount = overview.Totals.ActiveRuns;
        failedRunCount = overview.Totals.FailedRuns;
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
        Navigation.NavigateTo(
            BuildAgentsRoute(
                key,
                effectiveRequestedAgentId,
                string.Equals(key, "agents", StringComparison.Ordinal) ? effectiveRequestedTeamId : null),
            replace: true);
        return Task.CompletedTask;
    }

    private void ApplyRequestedTab()
    {
        var requestedTab = ResolveRequestedTab();
        effectiveRequestedAgentId = ResolveRequestedAgentId();
        effectiveRequestedTeamId = ResolveRequestedTeamId();
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

    private Guid? ResolveRequestedTeamId()
    {
        if (RequestedTeamId.HasValue)
        {
            return RequestedTeamId;
        }

        return Guid.TryParse(TryGetQueryValue("teamId"), out var teamId)
            ? teamId
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
        Guid? agentId,
        Guid? teamId)
    {
        if (!agentId.HasValue &&
            !teamId.HasValue &&
            string.Equals(tab, "overview", StringComparison.Ordinal))
        {
            return "/agents";
        }

        var query = new List<string>
        {
            $"tab={Uri.EscapeDataString(tab)}"
        };

        if (agentId.HasValue)
        {
            query.Add($"agentId={agentId.Value:D}");
        }

        if (teamId.HasValue)
        {
            query.Add($"teamId={teamId.Value:D}");
        }

        return $"/agents?{string.Join("&", query)}";
    }

    private string ResolveSummaryValue(int value)
    {
        return isLoaded ? value.ToString() : "...";
    }

    private string ResolveOverviewValue(int value)
    {
        return isLoaded ? AgentUsageDisplay.FormatCount(value) : "...";
    }

    private string ResolveOverviewTokens(int value)
    {
        return isLoaded ? AgentUsageDisplay.FormatTokens(value) : "...";
    }

    private string ResolveOverviewCost(decimal value)
    {
        return isLoaded ? AgentUsageDisplay.FormatCost(value) : "...";
    }

    private async Task OpenAgentUsageDialogAsync()
    {
        try
        {
            await DialogService.OpenAsync<AgentUsageDialog>(
                "Agent usage",
                options: new DialogOptions
                {
                    Eyebrow = "Usage analytics",
                    Subtitle = "Rank technical agents by executions, known usage, failed runs, and last activity.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Agent usage details",
                    TestId = "agents-usage-dialog-shell"
                });
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent usage failed", exception.Message);
        }
    }

    private async Task OpenProviderUsageDialogAsync()
    {
        try
        {
            await DialogService.OpenAsync<ProviderUsageDialog>(
                "Provider usage",
                options: new DialogOptions
                {
                    Eyebrow = "Provider distribution",
                    Subtitle = "Inspect provider usage, token totals, unknown observations, cost, and failed runs.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Provider usage details",
                    TestId = "provider-usage-dialog-shell"
                });
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider usage failed", exception.Message);
        }
    }

    private async Task OpenModelUsageDialogAsync()
    {
        try
        {
            await DialogService.OpenAsync<ModelUsageDialog>(
                "Model usage",
                options: new DialogOptions
                {
                    Eyebrow = "Model distribution",
                    Subtitle = "Inspect model-level usage without adding model detail to the default dashboard.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Model usage details",
                    TestId = "model-usage-dialog-shell"
                });
        }
        catch (Exception exception)
        {
            NotificationService.Error("Model usage failed", exception.Message);
        }
    }

    private Task OpenAgentsForTeamAsync(Guid teamId)
    {
        selectedTab = "agents";
        effectiveRequestedAgentId = null;
        effectiveRequestedTeamId = teamId;
        Navigation.NavigateTo(BuildAgentsRoute("agents", null, teamId));
        return Task.CompletedTask;
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

    private sealed record OverviewMetricBadge(
        string Label,
        string Value,
        string Icon,
        string Tone,
        string TooltipText,
        string TestId);
}
