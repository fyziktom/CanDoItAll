using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class AgentChatSession(
    IAgentFrameworkWorkspaceService workspaceService,
    IProviderRuntimeAdministrationService providerService,
    ILogger logger) : IDisposable {
    private CancellationTokenSource targetLifetime = new();
    private CancellationTokenSource? catalogRead;
    private CancellationTokenSource? workspaceRead;
    private string agentObservation = "";
    private bool disposed;

    public long Generation { get; private set; }
    public long Revision { get; private set; }
    public long AgentRevision { get; private set; }
    public Guid? DesiredAgentId { get; private set; }
    public Guid? DesiredSessionId { get; private set; }
    public AgentDefinition? Agent { get; private set; }
    public ChatAgentWorkspaceSnapshot? Workspace { get; private set; }
    public ImmutableArray<AgentDefinition> Agents { get; private set; } = [];
    public ImmutableArray<ProviderProfile> Providers { get; private set; } = [];
    public ImmutableHashSet<Guid> PrivateAgentIds { get; private set; } = [];
    public ImmutableArray<ExecutionLogEntry> ExecutionLog { get; private set; } = [];
    public ImmutableArray<AgentRunMetric> Metrics { get; private set; } = [];
    public bool Focused { get; private set; }
    public bool IsLoading => workspaceRead is not null || catalogRead is not null;
    public string ErrorMessage { get; private set; } = "";
    public string ProviderWarning { get; private set; } = "";
    public CancellationToken TargetCancellation => targetLifetime.Token;

    public bool IsCurrent(long generation) => !disposed && Generation == generation;

    public bool MatchesAccepted(Guid? agentId, Guid? sessionId) => !disposed && Agent?.Id == agentId
        && Workspace is not null && Workspace.SelectedSessionId == sessionId;

    public async Task<bool> LoadAsync(Guid? agentId, Guid? sessionId, bool focused,
        AgentDefinition? preferredAgent = null, bool refreshCatalog = true, Guid? runId = null) {
        if (disposed) {
            return false;
        }
        var agentChanged = DesiredAgentId != agentId;
        var selectionChanged = agentChanged || DesiredSessionId != sessionId;
        ReplaceLifetime();
        var generation = Generation;
        DesiredAgentId = agentId;
        DesiredSessionId = sessionId;
        Focused = focused;
        ErrorMessage = "";
        if (selectionChanged) {
            ClearWorkspace(clearAgent: agentChanged);
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(targetLifetime.Token);
        workspaceRead = request;
        try {
            var trustedPreferred = focused && preferredAgent is { Status: AgentLifecycleStatus.Active, IsTemplate: false }
                && preferredAgent.Id == agentId;
            if (trustedPreferred) {
                Agents = [CopyAgent(preferredAgent!)];
            } else if (refreshCatalog || Agents.IsEmpty) {
                if (!await RefreshCatalogAsync(generation)) {
                    return false;
                }
            }
            if (!IsCurrent(generation)) {
                return false;
            }
            var target = agentId ?? (focused ? null : Agent?.Id ?? Agents.FirstOrDefault()?.Id);
            var nextAgent = Agents.FirstOrDefault(item => item.Id == target);
            if (nextAgent is null || focused && nextAgent.Status != AgentLifecycleStatus.Active) {
                ClearWorkspace();
                ErrorMessage = agentId.HasValue || focused ? "The requested agent is not available." : "";
                Revision++;
                return false;
            }
            DesiredAgentId = nextAgent.Id;
            var nextWorkspace = CopyWorkspace(await workspaceService.GetChatAgentWorkspaceAsync(nextAgent.Id, sessionId, request.Token));
            if (!IsCurrent(generation)) {
                return false;
            }
            if (nextWorkspace.AgentId != nextAgent.Id || sessionId.HasValue && (nextWorkspace.SelectedSessionId != sessionId || nextWorkspace.SelectedSession is null)
                || nextWorkspace.SelectedSession is { } selected && (selected.AgentId != nextAgent.Id || selected.Id != nextWorkspace.SelectedSessionId)) {
                ErrorMessage = "The requested thread is not available.";
                if (selectionChanged) {
                    ClearWorkspace();
                }
                Revision++;
                return false;
            }
            ExecutionRunDetail? detail = null;
            var selectedRunId = runId ?? nextWorkspace.SelectedRun?.Id;
            if (selectedRunId.HasValue) {
                detail = await workspaceService.GetExecutionRunDetailAsync(selectedRunId.Value, request.Token);
                if (!IsCurrent(generation)) {
                    return false;
                }
                if (detail.Run.Id != selectedRunId || detail.Run.AgentId != nextAgent.Id
                    || detail.Run.ChatSessionId != nextWorkspace.SelectedSessionId) {
                    ErrorMessage = "The requested run is not available for this thread.";
                    Revision++;
                    return false;
                }
                nextWorkspace = nextWorkspace with { SelectedRun = detail.Run with { PendingApprovals = detail.Run.PendingApprovals.ToImmutableArray() } };
            }
            AcceptAgent(nextAgent);
            Workspace = nextWorkspace;
            ExecutionLog = detail?.ExecutionLog.ToImmutableArray() ?? [];
            Metrics = detail?.Metrics.ToImmutableArray() ?? [];
            Revision++;
            return true;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (IsCurrent(generation)) {
                ErrorMessage = "The agent workspace could not be loaded. Refresh to try again.";
                LogFailure(exception, "workspace");
                Revision++;
            }
            return false;
        } finally {
            if (ReferenceEquals(workspaceRead, request)) {
                workspaceRead = null;
            }
        }
    }

    public async Task<bool> RefreshCatalogAsync(long? expectedGeneration = null) {
        if (disposed || expectedGeneration.HasValue && !IsCurrent(expectedGeneration.Value)) {
            return false;
        }
        var generation = Generation;
        catalogRead?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(targetLifetime.Token);
        catalogRead = request;
        try {
            var nextAgents = (await workspaceService.ListAgentsAsync(includeTemplates: false, request.Token))
                .Where(agent => !agent.IsTemplate).Select(CopyAgent).ToImmutableArray();
            if (!OwnsCatalog(request, generation)) {
                return false;
            }
            var nextProviders = Providers;
            var warning = "";
            try {
                nextProviders = (await providerService.ListProvidersAsync(request.Token)).ToImmutableArray();
            } catch (OperationCanceledException) when (request.IsCancellationRequested) {
                return false;
            } catch (Exception exception) {
                warning = "Provider metadata could not be refreshed.";
                if (OwnsCatalog(request, generation)) {
                    LogFailure(exception, "provider metadata");
                }
            }
            if (!OwnsCatalog(request, generation)) {
                return false;
            }
            Agents = nextAgents;
            Providers = nextProviders;
            ProviderWarning = warning;
            var privateProviders = Providers.Where(provider => provider.IsPrivateProvider).Select(provider => provider.Id).ToHashSet();
            PrivateAgentIds = Agents.Where(agent => agent.ProviderProfileId is { } id && privateProviders.Contains(id))
                .Select(agent => agent.Id).ToImmutableHashSet();
            if (Agent is { } current) {
                AcceptAgent(Agents.FirstOrDefault(agent => agent.Id == current.Id));
                if (Agent is null) {
                    Workspace = null;
                    ExecutionLog = [];
                    Metrics = [];
                    ErrorMessage = "The requested agent is not available.";
                }
            }
            Revision++;
            return true;
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (OwnsCatalog(request, generation)) {
                ErrorMessage = "The agent catalog could not be loaded. Refresh to try again.";
                LogFailure(exception, "agent catalog");
                Revision++;
            }
            return false;
        } finally {
            if (ReferenceEquals(catalogRead, request)) {
                catalogRead = null;
            }
        }
    }

    public bool TryObserveExecution(ExecutionLogEntry entry, long generation) {
        if (!IsCurrent(generation) || Agent?.Id != entry.AgentId || Workspace is null
            || Workspace.SelectedSessionId != entry.ChatSessionId) {
            return false;
        }
        ExecutionLog = ExecutionLog.Where(item => item.Id != entry.Id).Append(entry)
            .OrderByDescending(item => item.CreatedAtUtc).ToImmutableArray();
        Revision++;
        return true;
    }

    private bool OwnsCatalog(CancellationTokenSource request, long generation)
        => IsCurrent(generation) && ReferenceEquals(catalogRead, request) && !request.IsCancellationRequested;

    private void AcceptAgent(AgentDefinition? agent) {
        var observation = agent is null ? "" : JsonSerializer.Serialize(agent);
        if (agentObservation != observation) {
            agentObservation = observation;
            AgentRevision++;
        }
        Agent = agent;
    }

    private void ClearWorkspace(bool clearAgent = true) {
        if (clearAgent) {
            AcceptAgent(null);
        }
        Workspace = null;
        ExecutionLog = [];
        Metrics = [];
    }

    private static AgentDefinition CopyAgent(AgentDefinition agent)
        => agent with { Tags = agent.Tags.ToImmutableArray(), Capabilities = agent.Capabilities.ToImmutableArray() };

    private static ChatAgentWorkspaceSnapshot CopyWorkspace(ChatAgentWorkspaceSnapshot value)
        => value with {
            Sessions = value.Sessions.ToImmutableArray(),
            SelectedSession = value.SelectedSession is { } session ? session with { Messages = session.Messages.ToImmutableArray() } : null,
            SelectedRun = value.SelectedRun is { } run ? run with { PendingApprovals = run.PendingApprovals.ToImmutableArray() } : null
        };

    private void ReplaceLifetime() {
        Generation++;
        var previous = targetLifetime;
        targetLifetime = new();
        catalogRead = null;
        workspaceRead = null;
        previous.Cancel();
        previous.Dispose();
    }

    private void LogFailure(Exception exception, string operation)
        => logger.LogWarning("Agent chat {Operation} failed. AgentId={AgentId} SessionId={SessionId} FailureType={FailureType}",
            operation, DesiredAgentId, DesiredSessionId, exception.GetType().Name);

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Generation++;
        targetLifetime.Cancel();
        targetLifetime.Dispose();
        catalogRead = null;
        workspaceRead = null;
    }
}
