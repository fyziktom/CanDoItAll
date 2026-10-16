using CanDoItAll.AgentFramework.UI.Shell;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.Pages.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class AgentsHomePage : IDisposable {
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IAgentsWorkspaceQuery WorkspaceQuery { get; set; } = default!;

    [Inject]
    private IDatabaseSwitchNotificationService DatabaseChanges { get; set; } = default!;

    [Inject]
    private TimeProvider Clock { get; set; } = default!;

    [Inject]
    public IAgentChatLauncher AgentChatLauncher { get; set; } = default!;

    [Inject]
    private AgentFrameworkCatalogWarmupService CatalogWarmupService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    private IAppNavigationHistory NavigationHistory { get; set; } = default!;

    [Inject]
    private ILogger<AgentsHomePage> Logger { get; set; } = default!;

    private readonly CancellationTokenSource lifetime = new();
    private UrlStateBinding<AgentsUrlState> url = default!;
    private AgentsOverviewSession session = default!;
    private bool disposed;
    private bool hasRendered;
    private CancellationTokenSource? overviewDialogs;
    private readonly HashSet<AgentsOverviewDetail> openDetails = [];
    private long dialogGeneration;
    private IDisposable? samePageDialogs;

    private AgentsShellState ShellPresentation => new(new(catalogScopeVersion, selectedTab),
        ResolveSummaryValue(technicalAgentCount), ResolveSummaryValue(providerCount), BoundResourceValue,
        ResolveSummaryValue(capabilityCount), ResolveSummaryValue(activeRunCount), ResolveSummaryValue(failedRunCount),
        HasHeaderFailure ? HeaderFailureText : null, session.HeaderLoading,
        HrAgentDisplayName, HrAgentAvatarImageUrl, IsHrReady, isOpeningHrAgent,
        Tabs, isConfirmingDefaults, isFeedingDefaults);

    private async Task HandleShellIntentAsync(AgentsShellIntent intent) {
        if (disposed || intent.Origin != new AgentsShellOrigin(catalogScopeVersion, selectedTab)) {
            return;
        }
        switch (intent) {
            case AgentsShellIntent.SelectTab tab:
                await HandleTabChangedAsync(tab.Key);
                break;
            case AgentsShellIntent.Command { Action: AgentsShellCommand.RetryHeader }:
                await RetryHeaderAsync();
                break;
            case AgentsShellIntent.Command { Action: AgentsShellCommand.OpenHrAgent }:
                await OpenHrAgentAsync();
                break;
            case AgentsShellIntent.Command { Action: AgentsShellCommand.FeedDefaults }:
                await FeedDefaultsAsync();
                break;
            case AgentsShellIntent.Command { Action: AgentsShellCommand.OpenCrmHrAgents }:
                OpenCrmHrAgents();
                break;
            case AgentsShellIntent.Command { Action: AgentsShellCommand.OpenWorkflows }:
                OpenWorkflows();
                break;
            case AgentsShellIntent.Command { Action: AgentsShellCommand.OpenProcesses }:
                OpenProcesses();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent));
        }
    }

    private AgentsOverviewState OverviewPresentation => AgentsOverviewPresentation.Create(
        session.Overview, session.AcceptedUsage, usageSelection, isOverviewLoading, isUsageLoading,
        session.OverviewError, session.UsageError, overviewConsumerAvatarImageUrls, usagePeriod) with {
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
    private AgentsUrlState route => url.State;
    private AgentChatContextAccessState selectionAccessState = AgentChatContextAccessState.Loading;
    private (ProviderUsageWorkloadSelection Selection, ProviderUsagePeriod Period) overviewUsage =
        (ProviderUsageWorkloadSelection.Both, ProviderUsagePeriod.SevenDays);
    private string selectedTab => route.Tab.ToTabKey();
    private Guid? effectiveRequestedAgentId => route.AgentId ?? (route.Tab == AgentWorkspaceSection.Agents ? selectedContextAgent?.Id : null);
    private Guid? effectiveRequestedTeamId => route.TeamId;
    private SimpleChatWorkspaceRouteState simpleChatRouteState => route.SimpleChat;
    private ProviderHistoryLocation? historyLocation => route.History is { IsAnchored: true } history ? history.ToLocation() : null;
    private AgentDefinition? selectedContextAgent;
    private AgentTeamDefinition? selectedContextTeam;
    private AgentsHeaderAgent? hrAgent => session.Header?.HrAgent;
    private bool isConfirmingDefaults;
    private bool isFeedingDefaults;
    private bool isOpeningHrAgent;
    private AgentRuntimeSummary overview => session.Overview ?? AgentRuntimeSummary.Empty;
    private ProviderUsageWorkloadSelection usageSelection => route.UsageSelection;
    private ProviderUsagePeriod usagePeriod => route.UsagePeriod;
    private bool isUsageLoading => !hasRendered || session.UsageLoading;
    private bool isOverviewLoading => !hasRendered || session.OverviewLoading;
    private bool hasUsageLoaded => session.GetAcceptedUsage(usageSelection, usagePeriod) is not null;
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
                new(AgentsUrlCodec.TabKey, TabQuery),
                new(AgentsUrlCodec.AgentKey, AgentIdQuery),
                new(AgentsUrlCodec.TeamKey, TeamIdQuery)
            ]);

    private AgentChatContextAccessState AgentChatAccessState
        => route.Tab.UsesAgentSelection() ? selectionAccessState : AgentChatContextAccessState.Ready;

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
        url = new(Navigation, AgentsUrlCodec.Instance, NavigationHistory, typeof(AgentsHomePage));
        samePageDialogs = DialogService.PreserveDialogsOnSamePageNavigation();
        session = new(WorkspaceQuery, LoggerFactory.CreateLogger<AgentsOverviewSession>(),
            () => disposed ? Task.CompletedTask : InvokeAsync(() => {
                if (!disposed) {
                    StateHasChanged();
                }
            }), Clock);
        DatabaseChanges.Changed += OnDatabaseChanged;
    }

    [Inject]
    private ILoggerFactory LoggerFactory { get; set; } = default!;

    private long catalogScopeVersion;

    private void OnDatabaseChanged(object? sender, DatabaseProfileChangedNotification notification) {
        catalogScopeVersion++;
        session.InvalidateContext();
        _ = InvokeAsync(() => {
            if (!disposed) {
                CloseOverviewDialogs();
                StateHasChanged();
            }
        });
    }

    protected override Task OnParametersSetAsync() {
        var change = url.Apply(canonicalize: RendererInfo.IsInteractive);
        if (change.IsChanged) {
            ApplyRouteChange(change.Previous, change.Current, change.IsInitial);
        }
        if (RendererInfo.IsInteractive && route.History is { IsAnchored: false } history) {
            Go(route with { History = history.AnchoredAt(Clock.GetUtcNow()) }, UrlHistoryMode.Replace);
        }
        return hasRendered ? session.EnsureAsync(route.Tab, usageSelection, usagePeriod) : Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender || disposed) {
            return;
        }
        hasRendered = true;
        var load = session.EnsureAsync(route.Tab, usageSelection, usagePeriod);
        StateHasChanged();
        await load;
    }

    private Task RetryOverviewAsync() => session.RetryOverviewAsync();
    private Task RetryUsageAsync() => session.RetryUsageAsync(usageSelection, usagePeriod);
    private Task RetryHeaderAsync() => session.RetryHeaderAsync();

    private async Task FeedDefaultsAsync() {
        if (disposed) {
            return;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
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
                }, cancellationToken: request.Token);
            if (disposed) {
                return;
            }
            isConfirmingDefaults = false;
            if (confirmed is not true) {
                return;
            }

            isFeedingDefaults = true;
            ClearStatusMessage();
            await CatalogWarmupService.WarmupAsync(request.Token);
            if (disposed) {
                return;
            }
            await session.RefreshDemandedAsync(route.Tab, usageSelection, usagePeriod);
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
        var tab = AgentWorkspaceSections.FromTabKey(key);
        if (tab == route.Tab) {
            return Task.CompletedTask;
        }
        var carriedAgent = tab.UsesAgentSelection() && tab != AgentWorkspaceSection.Agents
            ? route.Tab == AgentWorkspaceSection.Agents ? selectedContextAgent?.Id : route.AgentId
            : null;
        Go(new AgentsUrlState {
            Tab = tab,
            AgentId = carriedAgent,
            UsageSelection = overviewUsage.Selection,
            UsagePeriod = overviewUsage.Period
        }, UrlHistoryMode.Push);
        return Task.CompletedTask;
    }

    private Task HandleCatalogAgentChangedAsync(AgentDefinition? agent) {
        selectedContextAgent = agent;
        return Task.CompletedTask;
    }

    private Task HandlePanelAgentChangedAsync(AgentDefinition? agent) {
        selectedContextAgent = agent;
        if (agent is null || agent.Id == route.AgentId) {
            return Task.CompletedTask;
        }
        Go(route with { AgentId = agent.Id }, route.AgentId is null ? UrlHistoryMode.Replace : UrlHistoryMode.Push);
        return Task.CompletedTask;
    }

    private Task HandleSelectedTeamChangedAsync(AgentTeamDefinition? team) {
        selectedContextTeam = team;
        if (team is null && route.TeamId is not null && route.Tab == AgentWorkspaceSection.Agents) {
            Go(route with { TeamId = null }, UrlHistoryMode.Replace);
        }
        return Task.CompletedTask;
    }

    private Task HandleTeamSelectionRequestedAsync(Guid? teamId) {
        if (teamId != route.TeamId) {
            Go(route with { TeamId = teamId }, UrlHistoryMode.Push);
        }
        return Task.CompletedTask;
    }

    private Task HandleAgentOpenRequestedAsync(Guid agentId) {
        var previous = route;
        url.OpenOverlay(route with { Tab = AgentWorkspaceSection.Agents, AgentId = agentId, AgentSection = AgentEditorSection.Identity });
        ApplyRouteChange(previous, route, initial: false);
        return Task.CompletedTask;
    }

    private Task HandleAgentSectionChangedAsync(AgentEditorSection section) {
        if (route.OpenAgentId is not null && section != route.AgentSection) {
            Go(route with { AgentSection = section }, UrlHistoryMode.Replace);
        }
        return Task.CompletedTask;
    }

    private async Task HandleAgentCloseRequestedAsync() {
        if (route.OpenAgentId is null) {
            return;
        }
        var previous = route;
        await url.CloseOverlayAsync(route with { AgentId = null, AgentSection = AgentEditorSection.Identity });
        ApplyRouteChange(previous, route, initial: false);
    }

    private Task HandleSelectionAccessStateChangedAsync(AgentChatContextAccessState state) {
        if (!Enum.IsDefined(state)) {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The Agents selection access state is undefined.");
        }

        selectionAccessState = state;
        return Task.CompletedTask;
    }

    private Task HandleProviderRequestedAsync(ProviderRouteSelection requested) {
        if (requested.ProviderId != route.ProviderId || requested.Section != route.ProviderSection) {
            Go(route with { ProviderId = requested.ProviderId, ProviderSection = requested.Section, Dialog = null, History = null },
                UrlHistoryMode.Push);
        }
        return Task.CompletedTask;
    }

    private Task HandleProviderPublishedAsync(ProviderRouteSelection published) {
        if (published.ProviderId != route.ProviderId || published.Section != route.ProviderSection) {
            Go(route with { ProviderId = published.ProviderId, ProviderSection = published.Section, History = null },
                UrlHistoryMode.Replace);
        }
        return Task.CompletedTask;
    }

    private Task HandleNewProviderDraftRequestedAsync() {
        if (route.ProviderId is not null) {
            Go(route with { ProviderId = null, ProviderSection = ProviderEditorSection.Connection, Dialog = null, History = null },
                UrlHistoryMode.Push);
        }
        return Task.CompletedTask;
    }

    private Task HandleProviderSectionRequestedAsync(ProviderEditorSection section) {
        if (route.ProviderId is not null && section != route.ProviderSection) {
            Go(route with { ProviderSection = section, History = null }, UrlHistoryMode.Push);
        }
        return Task.CompletedTask;
    }

    private async Task HandleConnectionsRequestedAsync(bool open) {
        if (open == route.IsSharedConnectionsOpen) {
            return;
        }
        if (open) {
            url.OpenOverlay(route with { Dialog = AgentsPageDialog.SharedConnections });
        } else {
            await url.CloseOverlayAsync(route with { Dialog = null });
        }
        StateHasChanged();
    }

    private async Task HandleHistoryLocationChangedAsync(ProviderHistoryLocationChange change) {
        var next = route with { History = AgentsHistoryUrlState.FromLocation(change.Location) };
        switch (change.Kind) {
            case ProviderHistoryLocationChangeKind.OpenEntry:
                url.OpenOverlay(next);
                break;
            case ProviderHistoryLocationChangeKind.CloseEntry:
                await url.CloseOverlayAsync(next);
                break;
            default:
                url.Navigate(next, UrlHistoryMode.Push);
                break;
        }
        StateHasChanged();
    }

    private void Go(AgentsUrlState next, UrlHistoryMode mode) {
        var previous = route;
        url.Navigate(next, mode);
        ApplyRouteChange(previous, route, initial: false);
    }

    private void ApplyRouteChange(AgentsUrlState previous, AgentsUrlState current, bool initial) {
        var tabChanged = initial || previous.Tab != current.Tab;
        if (tabChanged || current.AgentId is not null && selectedContextAgent?.Id != current.AgentId) {
            selectedContextAgent = null;
        }
        if (selectedContextTeam?.Id != current.TeamId) {
            selectedContextTeam = null;
        }
        if (tabChanged || previous.AgentId != current.AgentId || previous.TeamId != current.TeamId) {
            selectionAccessState = current.Tab.UsesAgentSelection() ? AgentChatContextAccessState.Loading : AgentChatContextAccessState.Ready;
        }
        if (!initial && previous.Tab == AgentWorkspaceSection.Overview &&
            (current.Tab != AgentWorkspaceSection.Overview || current.UsageSelection != previous.UsageSelection || current.UsagePeriod != previous.UsagePeriod)) {
            CloseOverviewDialogs();
        }
        if (current.Tab == AgentWorkspaceSection.Overview) {
            overviewUsage = (current.UsageSelection, current.UsagePeriod);
        }
    }

    private string ResolveSummaryValue(int value) {
        return hasOverviewLoaded ? value.ToString() : isOverviewLoading ? "..." : "\u2014";
    }

    private Task HandleOverviewIntentAsync(AgentsOverviewIntent intent) {
        if (disposed) {
            return Task.CompletedTask;
        }
        return intent switch {
            AgentsOverviewIntent.SelectPeriod change => HandleUsagePeriodChangedAsync(change.Period),
            AgentsOverviewIntent.SelectUsage change => HandleUsageScopeChangedAsync(change.Selection),
            AgentsOverviewIntent.RetryOverview => RetryOverviewAsync(),
            AgentsOverviewIntent.RetryUsage => RetryUsageAsync(),
            AgentsOverviewIntent.RetryHeader => RetryHeaderAsync(),
            AgentsOverviewIntent.OpenDetail detail => OpenUsageDialogAsync(detail.Detail, detail.Query),
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
        Go(route with { UsageSelection = selection }, UrlHistoryMode.Push);
        await session.EnsureAsync(route.Tab, usageSelection, usagePeriod);
    }

    private async Task HandleUsagePeriodChangedAsync(ProviderUsagePeriod period) {
        if (!Enum.IsDefined(period)) {
            throw new ArgumentOutOfRangeException(nameof(period));
        }
        if (disposed || period == usagePeriod) {
            return;
        }
        Go(route with { UsagePeriod = period }, UrlHistoryMode.Push);
        await session.EnsureAsync(route.Tab, usageSelection, usagePeriod);
    }

    private async Task HandleSimpleChatRouteStateChangedAsync(SimpleChatWorkspaceRouteState state) {
        ArgumentNullException.ThrowIfNull(state);
        var current = route.SimpleChat;
        if (state == current) {
            return;
        }
        var next = route with { SimpleChat = state };
        if (state.View != current.View) {
            Go(next, UrlHistoryMode.Push);
            return;
        }
        if (state.View == SimpleChatWorkspaceView.Definitions) {
            if (state.DefinitionId is null) {
                await url.CloseOverlayAsync(next);
            } else if (current.DefinitionId is null) {
                url.OpenOverlay(next);
            } else {
                url.Navigate(next, UrlHistoryMode.Push);
            }
            StateHasChanged();
            return;
        }
        Go(next, current.ConversationId is null || state.ConversationId is null ? UrlHistoryMode.Replace : UrlHistoryMode.Push);
    }

    private async Task OpenUsageDialogAsync(AgentsOverviewDetail detail, ProviderUsageQuery query) {
        if (disposed || route.Tab != AgentWorkspaceSection.Overview || query != session.GetAcceptedUsage(usageSelection, usagePeriod)?.Query
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
                new Dictionary<string, object?> { [nameof(AgentUsageDialog.Query)] = query, [nameof(AgentUsageDialog.OwnerLifetime)] = token },
                new DialogOptions {
                    Eyebrow = "Usage analytics",
                    Subtitle = $"{query.Selection} · {query.Period.Label()} · {query.Window.FromUtc:u} ≤ usage < {query.Window.ToUtc:u}. {subtitle}",
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
        Go(new AgentsUrlState { Tab = AgentWorkspaceSection.Agents, TeamId = teamId }, UrlHistoryMode.Push);
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
        if (disposed) {
            return;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        if (disposed || isOpeningHrAgent || !IsHrReady ||
            hrAgent is null || hrAgent.Id != HrAgentIdentity.AgentId) {
            return;
        }

        isOpeningHrAgent = true;
        try {
            await AgentChatLauncher.StartNewChatAsync(hrAgent.Id, request.Token);
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
        DatabaseChanges.Changed -= OnDatabaseChanged;
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
