using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class AgentsHomePage : IDisposable {
    private const string AgentFrameworkShellHelpText =
        "This shell owns the technical agent catalog, durable execution evidence, and provider diagnostics. CRM-HR consumes that catalog through its business-facing directory and bridge surfaces, while Processes and Collaboration stay canonical for launch, run, and approval governance.";

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IAgentsWorkspaceQuery WorkspaceQuery { get; set; } = default!;

    [Inject]
    public IAgentChatLauncher AgentChatLauncher { get; set; } = default!;

    [Inject]
    private AgentFrameworkCatalogWarmupService CatalogWarmupService { get; set; } = default!;

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

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.SimpleChatViewQueryKey)]
    public string? RequestedSimpleChatView { get; set; }

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.DefinitionIdQueryKey)]
    public string? RequestedDefinitionId { get; set; }

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.ConversationIdQueryKey)]
    public string? RequestedConversationId { get; set; }

    [SupplyParameterFromQuery(Name = AgentWorkspaceRouteState.UsageScopeQueryKey)]
    public string? RequestedUsageScope { get; set; }

    [Inject]
    private ILogger<AgentsHomePage> Logger { get; set; } = default!;

    private readonly CancellationTokenSource lifetime = new();
    private AgentsOverviewSession session = default!;
    private bool disposed;
    private bool hasRendered;
    private CancellationTokenSource? overviewDialogs;
    private readonly HashSet<AgentsOverviewDetail> openDetails = [];
    private long dialogGeneration;
    private IDisposable? samePageDialogs;

    private AgentsOverviewState OverviewPresentation => AgentsOverviewPresentation.Create(
        session.Overview, session.AcceptedUsage, usageSelection, isOverviewLoading, isUsageLoading,
        session.OverviewError, session.UsageError, overviewConsumerAvatarImageUrls) with {
            HeaderWarning = HasHeaderFailure ? HeaderFailureText : null,
            HeaderLoading = session.HeaderLoading,
            OpenDetails = openDetails.ToImmutableHashSet()
        };
    private int technicalAgentCount => overview.Totals.AgentCount;
    private int providerCount => overview.Totals.ProviderCount;
    private int? boundResourceCount => session.Header?.BoundResourceCount;
    private int capabilityCount => overview.Totals.CapabilityCount;
    private int activeRunCount => overview.Totals.ActiveRuns;
    private int failedRunCount => overview.Totals.FailedRuns;
    private AgentsWorkspaceState workspaceState = new();
    private string selectedTab => workspaceState.Section.ToTabKey();
    private Guid? effectiveRequestedAgentId => workspaceState.AgentId;
    private Guid? effectiveRequestedTeamId => workspaceState.TeamId;
    private SimpleChatWorkspaceRouteState simpleChatRouteState => workspaceState.SimpleChat;
    private AgentDefinition? selectedContextAgent;
    private AgentTeamDefinition? selectedContextTeam;
    private AgentsHeaderAgent? hrAgent => session.Header?.HrAgent;
    private AgentChatContextAccessState selectionAccessState => workspaceState.SelectionAccess;
    private bool isConfirmingDefaults;
    private bool isFeedingDefaults;
    private bool isOpeningHrAgent;
    private AgentOverviewSnapshot overview => session.Overview ?? AgentOverviewSnapshot.Empty;
    private ProviderUsageWorkloadSelection usageSelection => workspaceState.UsageSelection;
    private bool isUsageLoading => !hasRendered || session.UsageLoading;
    private bool isOverviewLoading => !hasRendered || session.OverviewLoading;
    private bool hasUsageLoaded => session.GetAcceptedUsage(usageSelection) is not null;
    private bool hasOverviewLoaded => session.Overview is not null;
    private bool CanOpenUsage => hasUsageLoaded && !isUsageLoading;
    private IReadOnlyDictionary<string, string?> overviewConsumerAvatarImageUrls
        => session.Header?.AvatarImageUrls ?? System.Collections.Immutable.ImmutableDictionary<string, string?>.Empty;
    private bool IsHrReady => session.HeaderError is null && hrAgent is not null
        && !session.Header!.Failures.HasFlag(AgentsHeaderFailure.HrAgent);
    private bool HasHeaderFailure => session.HeaderError is not null || session.Header?.Failures is not (null or AgentsHeaderFailure.None);
    private string HeaderFailureText => session.HeaderError ?? string.Join(" ", new[] {
        session.Header?.Failures.HasFlag(AgentsHeaderFailure.HrAgent) == true ? "HR Agent information is unavailable." : null,
        session.Header?.Failures.HasFlag(AgentsHeaderFailure.Avatars) == true ? "Agent avatars could not be refreshed." : null,
        session.Header?.Failures.HasFlag(AgentsHeaderFailure.BoundResources) == true ? "Bound-resource information could not be refreshed." : null
    }.Where(value => value is not null));
    private string BoundResourceValue => boundResourceCount is { } count
        ? count + (session.Header?.Failures.HasFlag(AgentsHeaderFailure.BoundResources) == true ? " \u00b7 stale" : string.Empty)
        : !hasRendered || session.HeaderLoading ? "..." : "\u2014";

    private AgentChatContextSurface AgentChatSurface
        => AgentFrameworkAgentsChatContextBuilder.Build(
            AgentFrameworkAgentsChatContextBuilder.ResolveView(selectedTab),
            effectiveRequestedAgentId,
            effectiveRequestedTeamId,
            technicalAgentCount,
            providerCount,
            session.HeaderError is null && session.Header?.Failures.HasFlag(AgentsHeaderFailure.BoundResources) != true
                ? boundResourceCount : null,
            capabilityCount,
            activeRunCount,
            failedRunCount,
            selectedContextAgent,
            selectedContextTeam,
            includeSummaryFacts: hasOverviewLoaded);

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
        => workspaceState.Section.UsesAgentSelection() ? selectionAccessState : AgentChatContextAccessState.Ready;

    private string HrAgentDisplayName
        => hrAgent?.Name ?? HrAgentIdentity.DefaultDisplayName;

    private string HrAgentAvatarImageUrl
        => hrAgent?.AvatarImageUrl ?? HrAgentIdentity.DefaultAvatarImageUrl;

    private IReadOnlyList<SecondaryTabItem> Tabs =>
    [
        new(AgentWorkspaceTabs.Overview, "Overview"),
        new(AgentWorkspaceTabs.Agents, "Agents", ResolveSummaryValue(technicalAgentCount)),
        new(AgentWorkspaceTabs.SimpleChats, "Simple Chats"),
        new(AgentWorkspaceTabs.Providers, "Providers", ResolveSummaryValue(providerCount)),
        new(AgentWorkspaceTabs.RequestHistory, "Request history"),
        new(AgentWorkspaceTabs.Voice, "Voice"),
        new(AgentWorkspaceTabs.FloatingChat, "Floating chat"),
        new(AgentWorkspaceTabs.Chat, "Chat"),
        new(AgentWorkspaceTabs.Capabilities, "Capabilities", ResolveSummaryValue(capabilityCount)),
        new(AgentWorkspaceTabs.Governance, "Governance", ResolveSummaryValue(activeRunCount)),
        new(AgentWorkspaceTabs.Diagnostics, "Diagnostics", ResolveSummaryValue(failedRunCount))
    ];

    protected override void OnInitialized() {
        samePageDialogs = DialogService.PreserveDialogsOnSamePageNavigation();
        session = new(WorkspaceQuery, LoggerFactory.CreateLogger<AgentsOverviewSession>(),
            () => disposed ? Task.CompletedTask : InvokeAsync(() => {
                if (!disposed) {
                    StateHasChanged();
                }
            }));
    }

    [Inject]
    private ILoggerFactory LoggerFactory { get; set; } = default!;

    protected override Task OnParametersSetAsync() {
        ApplyRequestedTab();
        return hasRendered ? session.EnsureAsync(workspaceState.Section, usageSelection) : Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender || disposed) {
            return;
        }
        hasRendered = true;
        var load = session.EnsureAsync(workspaceState.Section, usageSelection);
        StateHasChanged();
        await load;
    }

    private Task RetryOverviewAsync() => session.RetryOverviewAsync();
    private Task RetryUsageAsync() => session.RetryUsageAsync(usageSelection);
    private Task RetryHeaderAsync() => session.RetryHeaderAsync();

    private async Task FeedDefaultsAsync() {
        if (disposed || isConfirmingDefaults || isFeedingDefaults) {
            return;
        }

        isConfirmingDefaults = true;

        try {
            var confirmed = await DialogService.OpenAsync<AgentDefaultsConfirmationDialog>(
                "Load default agents and providers?",
                options: new DialogOptions {
                    Eyebrow = "Managed defaults",
                    Subtitle = "Confirm before synchronizing the AgentFramework catalog.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    AriaLabel = "Confirm loading default agents and providers",
                    TestId = "agents-feed-defaults-confirmation"
                }, cancellationToken: lifetime.Token);
            if (disposed) {
                return;
            }
            isConfirmingDefaults = false;
            if (confirmed is not true) {
                return;
            }

            isFeedingDefaults = true;
            ClearStatusMessage();
            await CatalogWarmupService.WarmupAsync(lifetime.Token);
            if (disposed) {
                return;
            }
            await session.RefreshDemandedAsync(workspaceState.Section, usageSelection);
            if (disposed) {
                return;
            }
            SetStatusMessage("Default agents, providers, capabilities, workflows, and CRM-HR projections were synchronized.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            if (!disposed) {
                Logger.LogWarning("Default catalog synchronization failed ({FailureType}).", exception.GetType().Name);
                SetStatusError("Default catalog synchronization could not finish. Check its current state before retrying.");
            }
        } finally {
            if (!disposed) {
                isConfirmingDefaults = false;
                isFeedingDefaults = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private Task HandleTabChangedAsync(string key) {
        if (key != selectedTab) {
            CloseOverviewDialogs();
        }
        workspaceState = workspaceState.SelectSection(AgentWorkspaceSections.FromTabKey(key));
        Navigation.NavigateTo(BuildCurrentRoute(), replace: true);
        return Task.CompletedTask;
    }

    private Task HandleSelectedAgentChangedAsync(AgentDefinition? agent) {
        workspaceState = workspaceState with { AgentId = agent?.Id };
        selectedContextAgent = agent;
        if (!string.Equals(selectedTab, AgentWorkspaceTabs.Agents, StringComparison.Ordinal)) {
            workspaceState = workspaceState with { TeamId = null };
            selectedContextTeam = null;
        }

        return Task.CompletedTask;
    }

    private Task HandleSelectedTeamChangedAsync(AgentTeamDefinition? team) {
        workspaceState = workspaceState with { TeamId = team?.Id };
        selectedContextTeam = team;
        return Task.CompletedTask;
    }

    private Task HandleSelectionAccessStateChangedAsync(AgentChatContextAccessState state) {
        if (!Enum.IsDefined(state)) {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The Agents selection access state is undefined.");
        }

        workspaceState = workspaceState with { SelectionAccess = state };
        return Task.CompletedTask;
    }

    private void ApplyRequestedTab() {
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
        if (selectedContextAgent?.Id != requestedAgentId) {
            selectedContextAgent = null;
        }

        if (selectedContextTeam?.Id != requestedTeamId) {
            selectedContextTeam = null;
        }

        var next = workspaceState.ApplyRoute(routeState);
        if (workspaceState.Section == AgentWorkspaceSection.Overview &&
            (next.Section != AgentWorkspaceSection.Overview || next.UsageSelection != usageSelection)) {
            CloseOverviewDialogs();
        }
        workspaceState = next;
    }

    private string? ResolveRequestedTab() {
        if (!string.IsNullOrWhiteSpace(RequestedTab)) {
            return RequestedTab;
        }

        return TryGetQueryValue("tab");
    }

    private Guid? ResolveRequestedAgentId() {
        if (RequestedAgentId.HasValue) {
            return RequestedAgentId;
        }

        return Guid.TryParse(TryGetQueryValue("agentId"), out var agentId)
            ? agentId
            : null;
    }

    private Guid? ResolveRequestedTeamId() {
        if (RequestedTeamId.HasValue) {
            return RequestedTeamId;
        }

        return Guid.TryParse(TryGetQueryValue("teamId"), out var teamId)
            ? teamId
            : null;
    }

    private string? TryGetQueryValue(
        string key) {
        var query = Navigation.ToAbsoluteUri(Navigation.Uri).Query;
        if (string.IsNullOrWhiteSpace(query)) {
            return null;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)) {
            var segments = pair.Split('=', 2);
            if (!string.Equals(Uri.UnescapeDataString(segments[0]), key, StringComparison.OrdinalIgnoreCase)) {
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
        Guid? teamId) {
        if (!agentId.HasValue &&
            !teamId.HasValue &&
            string.Equals(tab, AgentWorkspaceTabs.Overview, StringComparison.Ordinal)) {
            return "/agents";
        }

        var query = new List<string> {
            $"tab={Uri.EscapeDataString(tab)}"
        };

        if (agentId.HasValue) {
            query.Add($"agentId={agentId.Value:D}");
        }

        if (teamId.HasValue) {
            query.Add($"teamId={teamId.Value:D}");
        }

        return $"/agents?{string.Join("&", query)}";
    }

    private string ResolveSummaryValue(int value) {
        return hasOverviewLoaded ? value.ToString() : isOverviewLoading ? "..." : "\u2014";
    }

    private Task HandleOverviewIntentAsync(AgentsOverviewIntent intent) {
        if (disposed) {
            return Task.CompletedTask;
        }
        return intent switch {
            AgentsOverviewIntent.SelectUsage change => HandleUsageScopeChangedAsync(change.Selection),
            AgentsOverviewIntent.RetryOverview => RetryOverviewAsync(),
            AgentsOverviewIntent.RetryUsage => RetryUsageAsync(),
            AgentsOverviewIntent.RetryHeader => RetryHeaderAsync(),
            AgentsOverviewIntent.OpenDetail detail => OpenUsageDialogAsync(detail.Detail, detail.Selection),
            AgentsOverviewIntent.OpenTeam team => OpenAgentsForTeamAsync(team.TeamId),
            _ => throw new ArgumentOutOfRangeException(nameof(intent))
        };
    }

    private async Task HandleUsageScopeChangedAsync(ProviderUsageWorkloadSelection selection) {
        if (selection is not (ProviderUsageWorkloadSelection.Agents or ProviderUsageWorkloadSelection.SimpleChats or ProviderUsageWorkloadSelection.Both)) {
            throw new ArgumentOutOfRangeException(nameof(selection));
        }
        if (disposed || selection == usageSelection) {
            return;
        }
        CloseOverviewDialogs();
        workspaceState = workspaceState with { UsageSelection = selection };
        Navigation.NavigateTo(BuildCurrentRoute(), replace: true);
        await session.EnsureAsync(workspaceState.Section, usageSelection);
    }

    private Task HandleSimpleChatRouteStateChangedAsync(SimpleChatWorkspaceRouteState state) {
        ArgumentNullException.ThrowIfNull(state);
        workspaceState = workspaceState with { SimpleChat = state };
        Navigation.NavigateTo(BuildCurrentRoute(), replace: true);
        return Task.CompletedTask;
    }

    private string BuildCurrentRoute()
        => AgentWorkspaceRouteState.Build(workspaceState.ToRoute());

    private async Task OpenUsageDialogAsync(AgentsOverviewDetail detail, ProviderUsageWorkloadSelection selection) {
        if (disposed || workspaceState.Section != AgentWorkspaceSection.Overview || selection != usageSelection
            || !CanOpenUsage || !openDetails.Add(detail)) {
            return;
        }
        var (component, title, subtitle, testId) = detail switch {
            AgentsOverviewDetail.Consumers => (typeof(AgentUsageDialog), "Consumer usage", "Rank technical agents by executions, known usage, failed runs, and last activity.", "agents-usage-dialog-shell"),
            AgentsOverviewDetail.Providers => (typeof(ProviderUsageDialog), "Provider usage", "Inspect provider usage, token totals, unknown observations, cost, and failed runs.", "provider-usage-dialog-shell"),
            AgentsOverviewDetail.Models => (typeof(ModelUsageDialog), "Model usage", "Inspect model-level usage without adding model detail to the default dashboard.", "model-usage-dialog-shell"),
            _ => throw new ArgumentOutOfRangeException(nameof(detail))
        };
        overviewDialogs ??= new();
        var token = overviewDialogs.Token;
        var generation = dialogGeneration;
        try {
            await DialogService.OpenAsync(title, component,
                new Dictionary<string, object?> { [nameof(AgentUsageDialog.Selection)] = selection },
                new DialogOptions {
                    Eyebrow = "Usage analytics",
                    Subtitle = subtitle,
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = title + " details",
                    TestId = testId
                }, token);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (!disposed && generation == dialogGeneration) {
                Logger.LogWarning("Overview {Detail} dialog failed ({FailureType}).", detail, exception.GetType().Name);
                NotificationService.Error("Usage details unavailable", "The usage dialog could not be opened. Retry the selected scope.");
            }
        } finally {
            if (!disposed && generation == dialogGeneration) {
                openDetails.Remove(detail);
            }
        }
    }

    private void CloseOverviewDialogs() {
        dialogGeneration++;
        openDetails.Clear();
        var owner = overviewDialogs;
        overviewDialogs = null;
        if (owner is null) {
            return;
        }
        try {
            owner.Cancel();
        } finally {
            owner.Dispose();
        }
    }

    private Task OpenAgentsForTeamAsync(Guid teamId) {
        if (disposed || session.Overview?.TeamShortcuts.Any(team => team.TeamId == teamId) != true) {
            return Task.CompletedTask;
        }
        CloseOverviewDialogs();
        workspaceState = workspaceState with { Section = AgentWorkspaceSection.Agents, AgentId = null, TeamId = teamId };
        Navigation.NavigateTo(BuildAgentsRoute(AgentWorkspaceTabs.Agents, null, teamId));
        return Task.CompletedTask;
    }

    private void SetStatusMessage(string value) {
        NotificationService.Success("AgentFramework updated", value);
    }

    private void SetStatusError(string value) {
        NotificationService.Error("AgentFramework update failed", value);
    }

    private void ClearStatusMessage() {
    }

    private void OpenCrmHrAgents() {
        Navigation.NavigateTo("/crm-hr/agents");
    }

    private async Task OpenHrAgentAsync() {
        if (disposed || isOpeningHrAgent || !IsHrReady ||
            hrAgent is null || hrAgent.Id != HrAgentIdentity.AgentId) {
            return;
        }

        isOpeningHrAgent = true;
        try {
            await AgentChatLauncher.StartNewChatAsync(hrAgent.Id, lifetime.Token);
            if (disposed) {
                return;
            }
            NotificationService.Success("HR Agent ready", "Opened a new managed HR chat.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            if (!disposed) {
                Logger.LogWarning("HR Agent launch failed ({FailureType}).", exception.GetType().Name);
                NotificationService.Error("Unable to open HR Agent", "The HR chat could not be opened. Retry when the agent is available.");
            }
        } finally {
            if (!disposed) {
                isOpeningHrAgent = false;
            }
        }
    }

    private void OpenProcesses() {
        Navigation.NavigateTo("/processes");
    }

    private void OpenWorkflows() {
        Navigation.NavigateTo("/agents/workflows");
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        samePageDialogs?.Dispose();
        CloseOverviewDialogs();
        session.Dispose();
        try {
            lifetime.Cancel();
        } finally {
            lifetime.Dispose();
        }
    }

}
