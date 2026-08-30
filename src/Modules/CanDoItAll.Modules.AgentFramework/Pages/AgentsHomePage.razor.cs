using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AppComponents;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
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
        "This shell owns the technical agent catalog, durable execution evidence, and provider diagnostics. CRM-HR consumes that catalog through its business-facing directory and bridge surfaces, while Processes and Collaboration stay canonical for launch, run, and approval governance.";

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public ProviderUsageQueryService UsageQueryService { get; set; } = default!;

    [Inject]
    public IAgentChatLauncher AgentChatLauncher { get; set; } = default!;

    [Inject]
    private AgentFrameworkCatalogWarmupService CatalogWarmupService { get; set; } = default!;

    [Inject]
    public IDbContextFactory<AppDbContext> DbContextFactory { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public AppToolbarState ToolbarState { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [SupplyParameterFromQuery(Name = "agentId")]
    public Guid? RequestedAgentId { get; set; }

    [SupplyParameterFromQuery(Name = "teamId")]
    public Guid? RequestedTeamId { get; set; }

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.SimpleChatViewQueryKey)]
    public string? RequestedSimpleChatView { get; set; }

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.DefinitionIdQueryKey)]
    public string? RequestedDefinitionId { get; set; }

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.ConversationIdQueryKey)]
    public string? RequestedConversationId { get; set; }

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.UsageScopeQueryKey)]
    public string? RequestedUsageScope { get; set; }

    private int technicalAgentCount;
    private int providerCount;
    private int boundResourceCount;
    private int capabilityCount;
    private int activeRunCount;
    private int failedRunCount;
    private string selectedTab = AgentWorkspaceTabs.Overview;
    private Guid? effectiveRequestedAgentId;
    private Guid? effectiveRequestedTeamId;
    private SimpleChatWorkspaceRouteState simpleChatRouteState = SimpleChatWorkspaceRouteState.Default;
    private AgentDefinition? selectedContextAgent;
    private AgentTeamDefinition? selectedContextTeam;
    private AgentDefinition? hrAgent;
    private AgentChatContextAccessState selectionAccessState = AgentChatContextAccessState.Loading;
    private bool isLoaded;
    private bool isConfirmingDefaults;
    private bool isFeedingDefaults;
    private bool isOpeningHrAgent;
    private bool hasOverviewLoadError;
    private string? overviewLoadError;
    private AgentOverviewSnapshot overview = AgentOverviewSnapshot.Empty;
    private ProviderUsageWorkloadSelection usageSelection = ProviderUsageWorkloadSelection.Both;
    private ProviderUsageSnapshot usage = ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Both);
    private bool isUsageLoading;
    private bool refreshUsageFromRoute;
    private string? usageLoadError;
    private IReadOnlyDictionary<string, string?> overviewConsumerAvatarImageUrls =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    private AgentChatContextSurface AgentChatSurface
        => AgentFrameworkAgentsChatContextBuilder.Build(
            AgentFrameworkAgentsChatContextBuilder.ResolveView(selectedTab),
            effectiveRequestedAgentId,
            effectiveRequestedTeamId,
            technicalAgentCount,
            providerCount,
            boundResourceCount,
            capabilityCount,
            activeRunCount,
            failedRunCount,
            selectedContextAgent,
            selectedContextTeam);

    private AgentChatNavigationIdentity AgentChatNavigationFence
        => AgentChatNavigationIdentity.CreateForLocation(
            Navigation.BaseUri,
            Navigation.Uri,
            [
                new("tab", RequestedTab),
                new("agentId", RequestedAgentId?.ToString("D")),
                new("teamId", RequestedTeamId?.ToString("D"))
            ]);

    private AgentChatContextAccessState AgentChatAccessState
        => hasOverviewLoadError
            ? AgentChatContextAccessState.Failed
            : isLoaded
                ? UsesAgentSelection(selectedTab)
                    ? selectionAccessState
                    : AgentChatContextAccessState.Ready
                : AgentChatContextAccessState.Loading;

    private string HrAgentDisplayName
        => hrAgent?.Name ?? HrAgentIdentity.DefaultDisplayName;

    private string HrAgentAvatarImageUrl
        => hrAgent?.AvatarImageUrl ?? HrAgentIdentity.DefaultAvatarImageUrl;

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
        new(AgentWorkspaceTabs.Overview, "Overview"),
        new(AgentWorkspaceTabs.Agents, "Agents", ResolveSummaryValue(technicalAgentCount)),
        new(AgentWorkspaceTabs.SimpleChats, "Simple Chats"),
        new(AgentWorkspaceTabs.Providers, "Providers", ResolveSummaryValue(providerCount)),
        new(AgentWorkspaceTabs.Voice, "Voice"),
        new(AgentWorkspaceTabs.FloatingChat, "Floating chat"),
        new(AgentWorkspaceTabs.Chat, "Chat"),
        new(AgentWorkspaceTabs.Capabilities, "Capabilities", ResolveSummaryValue(capabilityCount)),
        new(AgentWorkspaceTabs.Governance, "Governance", ResolveSummaryValue(activeRunCount)),
        new(AgentWorkspaceTabs.Diagnostics, "Diagnostics", ResolveSummaryValue(failedRunCount))
    ];

    private string SelectedTabText
        => Tabs.FirstOrDefault(tab => string.Equals(tab.Key, selectedTab, StringComparison.Ordinal))?.Label
            ?? string.Empty;

    private IReadOnlyList<SecondaryTabItem> UsageScopeTabs =>
    [
        new(nameof(ProviderUsageWorkloadSelection.Agents), "Agents"),
        new(nameof(ProviderUsageWorkloadSelection.SimpleChats), "Chats"),
        new(nameof(ProviderUsageWorkloadSelection.Both), "Both")
    ];

    private string UsageScopeKey => usageSelection.ToString();

    private string UsageScopeLabel => usageSelection switch
    {
        ProviderUsageWorkloadSelection.Agents => "Agents",
        ProviderUsageWorkloadSelection.SimpleChats => "Chats",
        ProviderUsageWorkloadSelection.Both => "Agents and Chats",
        _ => throw new ArgumentOutOfRangeException(nameof(usageSelection), usageSelection, "Unknown usage scope.")
    };

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
            ResolveUsageValue(usage.Totals.UsageObservationCount),
            "monitor_heart",
            "info",
            $"Usage observations for {UsageScopeLabel}.",
            "agents-overview-metric-usage"),
        new(
            "Tokens",
            ResolveUsageTokens(usage.Totals.Tokens.TotalTokens),
            "token",
            "success",
            $"Known token usage for {UsageScopeLabel}.",
            "agents-overview-metric-tokens"),
        new(
            "Cost",
            ResolveUsageCost(),
            "paid",
            "danger",
            $"Known execution-time provider cost for {UsageScopeLabel}; unpriced observations are never treated as free.",
            "agents-overview-metric-cost")
    ];

    private IReadOnlyList<ProviderUsageProviderRow> OverviewProviderRows =>
        usage.Providers
            .OrderByDescending(item => item.Totals.UsageObservationCount)
            .Take(6)
            .ToArray();

    private IReadOnlyList<ProviderUsageConsumerRow> TopUsageConsumers =>
        usage.Consumers
            .OrderByDescending(item => item.Totals.ExecutionCount)
            .ThenByDescending(item => item.Totals.KnownCostUsd)
            .Take(5)
            .ToArray();

    private IReadOnlyList<ProviderUsageConsumerRow> TopFailingConsumers =>
        usage.Consumers
            .Where(item => item.Totals.FailedExecutionCount > 0)
            .OrderByDescending(item => item.Totals.FailedExecutionCount)
            .ThenByDescending(item => item.Totals.ExecutionCount)
            .Take(5)
            .ToArray();

    private IReadOnlyDictionary<string, string?> OverviewConsumerAvatarImageUrls =>
        overviewConsumerAvatarImageUrls;

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
                            item.Totals.UsageObservationCount))
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
                            item.Totals.UsageObservationCount))
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
        ToolbarState.SetTabText(SelectedTabText);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RefreshShellAsync();
            return;
        }

        if (!refreshUsageFromRoute)
        {
            return;
        }

        refreshUsageFromRoute = false;
        await LoadUsageSelectionAsync(usageSelection);
        await InvokeAsync(StateHasChanged);
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
        var overviewTask = WorkspaceService.GetAgentOverviewAsync();
        var usageTask = UsageQueryService.QueryAsync(usageSelection).AsTask();
        var hrAgentTask = TryResolveHrAgentAsync();
        var boundResourceCountTask = LoadBoundResourceCountAsync();
        await Task.WhenAll(overviewTask, usageTask, hrAgentTask, boundResourceCountTask);

        overview = await overviewTask;
        usage = await usageTask;
        usageLoadError = ResolveUsageSourceError(usage);
        var hrAgentResolution = await hrAgentTask;
        hrAgent = hrAgentResolution.Agent;
        overviewConsumerAvatarImageUrls = hrAgentResolution.Agents.ToDictionary(
            item => item.Id.ToString("D"),
            item => item.AvatarImageUrl,
            StringComparer.OrdinalIgnoreCase);
        if (hrAgentResolution.ErrorMessage is { } hrAgentError)
        {
            NotificationService.Warning("HR Agent unavailable", hrAgentError);
        }
        technicalAgentCount = overview.Totals.AgentCount;
        providerCount = overview.Totals.ProviderCount;
        capabilityCount = overview.Totals.CapabilityCount;
        activeRunCount = overview.Totals.ActiveRuns;
        failedRunCount = overview.Totals.FailedRuns;
        boundResourceCount = await boundResourceCountTask;

        isLoaded = true;
    }

    private async Task<(AgentDefinition? Agent, IReadOnlyList<AgentDefinition> Agents, string? ErrorMessage)> TryResolveHrAgentAsync()
    {
        try
        {
            var agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
            var agent = agents.SingleOrDefault(HrAgentIdentity.Matches);
            return agent is null
                ? (null, agents, $"The managed agent '{HrAgentIdentity.AgentId:D}' is not available.")
                : (agent, agents, null);
        }
        catch (Exception exception)
        {
            return (null, [], exception.Message);
        }
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
        if (isConfirmingDefaults || isFeedingDefaults)
        {
            return;
        }

        isConfirmingDefaults = true;

        try
        {
            var confirmed = await DialogService.OpenAsync<AgentDefaultsConfirmationDialog>(
                "Load default agents and providers?",
                options: new DialogOptions
                {
                    Eyebrow = "Managed defaults",
                    Subtitle = "Confirm before synchronizing the AgentFramework catalog.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    AriaLabel = "Confirm loading default agents and providers",
                    TestId = "agents-feed-defaults-confirmation"
                });
            isConfirmingDefaults = false;
            if (confirmed is not true)
            {
                return;
            }

            isFeedingDefaults = true;
            ClearStatusMessage();
            await CatalogWarmupService.WarmupAsync();
            await LoadDashboardAsync();
            SetStatusMessage("Default agents, providers, capabilities, workflows, and CRM-HR projections were synchronized.");
        }
        catch (Exception exception)
        {
            SetStatusError($"Failed to load default agents and providers. {exception.Message}");
        }
        finally
        {
            isConfirmingDefaults = false;
            isFeedingDefaults = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task HandleTabChangedAsync(
        string key)
    {
        if (!string.Equals(selectedTab, key, StringComparison.Ordinal))
        {
            selectionAccessState = UsesAgentSelection(key)
                ? AgentChatContextAccessState.Loading
                : AgentChatContextAccessState.Ready;
        }

        selectedTab = key;
        if (!string.Equals(key, AgentWorkspaceTabs.Agents, StringComparison.Ordinal))
        {
            effectiveRequestedTeamId = null;
        }

        Navigation.NavigateTo(BuildCurrentRoute(), replace: true);
        return Task.CompletedTask;
    }

    private Task HandleSelectedAgentChangedAsync(AgentDefinition? agent)
    {
        effectiveRequestedAgentId = agent?.Id;
        selectedContextAgent = agent;
        if (!string.Equals(selectedTab, AgentWorkspaceTabs.Agents, StringComparison.Ordinal))
        {
            effectiveRequestedTeamId = null;
            selectedContextTeam = null;
        }

        return Task.CompletedTask;
    }

    private Task HandleSelectedTeamChangedAsync(AgentTeamDefinition? team)
    {
        effectiveRequestedTeamId = team?.Id;
        selectedContextTeam = team;
        return Task.CompletedTask;
    }

    private Task HandleSelectionAccessStateChangedAsync(AgentChatContextAccessState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The Agents selection access state is undefined.");
        }

        selectionAccessState = state;
        return Task.CompletedTask;
    }

    private void ApplyRequestedTab()
    {
        var previousTab = selectedTab;
        var previousAgentId = effectiveRequestedAgentId;
        var previousTeamId = effectiveRequestedTeamId;
        var routeState = AgentWorkspaceRouteState.Parse(
            ResolveRequestedTab(),
            ResolveRequestedAgentId(),
            ResolveRequestedTeamId(),
            RequestedSimpleChatView ?? TryGetQueryValue(AgentWorkspaceRouteState.SimpleChatViewQueryKey),
            RequestedDefinitionId ?? TryGetQueryValue(AgentWorkspaceRouteState.DefinitionIdQueryKey),
            RequestedConversationId ?? TryGetQueryValue(AgentWorkspaceRouteState.ConversationIdQueryKey),
            RequestedUsageScope ?? TryGetQueryValue(AgentWorkspaceRouteState.UsageScopeQueryKey));
        var requestedAgentId = routeState.AgentId;
        var requestedTeamId = routeState.TeamId;
        if (selectedContextAgent?.Id != requestedAgentId)
        {
            selectedContextAgent = null;
        }

        if (selectedContextTeam?.Id != requestedTeamId)
        {
            selectedContextTeam = null;
        }

        effectiveRequestedAgentId = requestedAgentId;
        effectiveRequestedTeamId = requestedTeamId;
        selectedTab = routeState.Tab;
        simpleChatRouteState = routeState.SimpleChat;
        if (isLoaded &&
            !isUsageLoading &&
            usage.Selection != routeState.UsageSelection)
        {
            refreshUsageFromRoute = true;
        }

        usageSelection = routeState.UsageSelection;
        if (!string.Equals(previousTab, selectedTab, StringComparison.Ordinal) ||
            previousAgentId != effectiveRequestedAgentId ||
            previousTeamId != effectiveRequestedTeamId)
        {
            selectionAccessState = UsesAgentSelection(selectedTab)
                ? AgentChatContextAccessState.Loading
                : AgentChatContextAccessState.Ready;
        }
    }

    private static bool UsesAgentSelection(string tab)
        => tab is AgentWorkspaceTabs.Agents or
            AgentWorkspaceTabs.Chat or
            AgentWorkspaceTabs.Capabilities or
            AgentWorkspaceTabs.Governance;

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
            string.Equals(tab, AgentWorkspaceTabs.Overview, StringComparison.Ordinal))
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

    private string ResolveUsageValue(int value)
        => isLoaded && !isUsageLoading ? AgentUsageDisplay.FormatCount(value) : "...";

    private string ResolveUsageTokens(int value)
        => isLoaded && !isUsageLoading ? AgentUsageDisplay.FormatTokens(value) : "...";

    private string ResolveUsageCost()
    {
        if (!isLoaded || isUsageLoading)
        {
            return "...";
        }

        var knownCost = AgentUsageDisplay.FormatCost(usage.Totals.KnownCostUsd);
        return usage.Totals.UnpricedObservationCount == 0
            ? knownCost
            : usage.Totals.PricedObservationCount == 0
                ? "Unpriced"
                : $"{knownCost} + {usage.Totals.UnpricedObservationCount:N0} unpriced";
    }

    private async Task HandleUsageScopeChangedAsync(string key)
    {
        var selection = key switch
        {
            nameof(ProviderUsageWorkloadSelection.Agents) => ProviderUsageWorkloadSelection.Agents,
            nameof(ProviderUsageWorkloadSelection.SimpleChats) => ProviderUsageWorkloadSelection.SimpleChats,
            nameof(ProviderUsageWorkloadSelection.Both) => ProviderUsageWorkloadSelection.Both,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown usage scope key.")
        };
        if (selection == usageSelection)
        {
            return;
        }

        usageSelection = selection;
        isUsageLoading = true;
        Navigation.NavigateTo(BuildCurrentRoute(), replace: true);
        await LoadUsageSelectionAsync(selection);
    }

    private async Task LoadUsageSelectionAsync(ProviderUsageWorkloadSelection selection)
    {
        isUsageLoading = true;
        usageLoadError = null;
        try
        {
            usage = await UsageQueryService.QueryAsync(selection);
            usageLoadError = ResolveUsageSourceError(usage);
        }
        catch (Exception exception)
        {
            usageLoadError = exception.Message;
            NotificationService.Error("Usage scope failed", exception.Message);
        }
        finally
        {
            isUsageLoading = false;
        }
    }

    private Task HandleSimpleChatRouteStateChangedAsync(SimpleChatWorkspaceRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        simpleChatRouteState = state;
        Navigation.NavigateTo(BuildCurrentRoute(), replace: true);
        return Task.CompletedTask;
    }

    private string BuildCurrentRoute()
        => AgentWorkspaceRouteState.Build(new(
            selectedTab,
            effectiveRequestedAgentId,
            effectiveRequestedTeamId,
            simpleChatRouteState,
            usageSelection));

    private static string? ResolveUsageSourceError(ProviderUsageSnapshot snapshot)
    {
        var failures = snapshot.Sources
            .Where(source => source.State != ProviderUsageSourceState.Complete)
            .Select(source => source.Error?.Message ?? $"{source.SourceName} returned partial usage data.")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return failures.Length == 0 ? null : string.Join(' ', failures);
    }

    private async Task OpenAgentUsageDialogAsync()
    {
        try
        {
            await DialogService.OpenAsync<AgentUsageDialog>(
                "Consumer usage",
                new Dictionary<string, object?>
                {
                    [nameof(AgentUsageDialog.Selection)] = usageSelection
                },
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
                new Dictionary<string, object?>
                {
                    [nameof(ProviderUsageDialog.Selection)] = usageSelection
                },
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
                new Dictionary<string, object?>
                {
                    [nameof(ModelUsageDialog.Selection)] = usageSelection
                },
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
        selectedTab = AgentWorkspaceTabs.Agents;
        effectiveRequestedAgentId = null;
        effectiveRequestedTeamId = teamId;
        Navigation.NavigateTo(BuildAgentsRoute(AgentWorkspaceTabs.Agents, null, teamId));
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

    private async Task OpenHrAgentAsync()
    {
        if (isOpeningHrAgent ||
            hrAgent is null ||
            !HrAgentIdentity.Matches(hrAgent) ||
            AgentChatAccessState != AgentChatContextAccessState.Ready)
        {
            return;
        }

        isOpeningHrAgent = true;
        try
        {
            await AgentChatLauncher.StartNewChatAsync(hrAgent.Id);
            NotificationService.Success("HR Agent ready", "Opened a new managed HR chat.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Unable to open HR Agent", exception.Message);
        }
        finally
        {
            isOpeningHrAgent = false;
        }
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
