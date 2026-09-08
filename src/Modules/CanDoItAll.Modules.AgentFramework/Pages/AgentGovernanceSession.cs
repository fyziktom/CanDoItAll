using CanDoItAll.AgentFramework.UI.Governance;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public sealed class AgentGovernanceSession(
    IAgentGovernanceReads reads,
    ILogger<AgentGovernanceSession> logger,
    Func<Task>? changed = null) : IDisposable {
    private readonly ReadLane catalog = new(ReadKind.Catalog);
    private readonly ReadLane list = new(ReadKind.List);
    private readonly ReadLane detail = new(ReadKind.Detail);
    private bool initialized;
    private bool hasCatalog;
    private bool hasList;
    private bool disposed;
    private long manualRevision;

    public long TargetRevision { get; private set; }
    public long SelectionRevision => manualRevision;
    public Guid? DesiredAgentId { get; private set; }
    public Guid? AcceptedAgentId { get; private set; }
    public Guid? AcceptedListAgentId { get; private set; }
    public Guid? DesiredRunId { get; private set; }
    public Guid? AcceptedRunId => Detail?.Run.Id;
    public bool AgentResolved { get; private set; }
    public AgentDefinition? AcceptedAgent { get; private set; }
    public ImmutableArray<AgentDefinition> Agents { get; private set; } = [];
    public ImmutableArray<ExecutionRunRecord> Runs { get; private set; } = [];
    public ExecutionRunDetail? Detail { get; private set; }
    public GovernanceLaneState CatalogState => catalog.State;
    public GovernanceLaneState ListState => list.State;
    public GovernanceLaneState DetailState => detail.State;
    public bool IsBusy => catalog.Pending || list.Pending || detail.Pending;
    public AgentChatContextAccessState AccessState => AgentResolved
        ? AgentChatContextAccessState.Ready
        : catalog.Pending ? AgentChatContextAccessState.Loading : AgentChatContextAccessState.Failed;

    public Task SetAgentAsync(Guid? agentId) {
        if (disposed || initialized && DesiredAgentId == agentId) {
            return Task.CompletedTask;
        }
        initialized = true;
        TargetRevision++;
        DesiredAgentId = agentId;
        AcceptedAgentId = null;
        AcceptedListAgentId = null;
        AcceptedAgent = null;
        AgentResolved = false;
        DesiredRunId = null;
        manualRevision++;
        Runs = [];
        Detail = null;
        hasList = false;
        Cancel(catalog);
        Reset(list);
        Reset(detail);
        if (!hasCatalog) {
            return ReadCatalogAndRunsAsync();
        }
        ResolveAgent();
        return AgentResolved ? ReadRunsAsync(manualRevision) : NotifyAsync();
    }

    public Task RefreshAsync()
        => disposed || !initialized ? Task.CompletedTask : ReadCatalogAndRunsAsync();

    public Task RetryCatalogAsync() => RefreshAsync();

    public Task RetryRunsAsync()
        => disposed || !AgentResolved ? Task.CompletedTask : ReadRunsAsync(manualRevision);

    public Task RetryDetailAsync()
        => disposed || !AgentResolved || DesiredRunId is null ? Task.CompletedTask : ReadDetailAsync();

    public Task SelectRunAsync(Guid runId) {
        if (disposed || !AgentResolved || !hasList || Runs.All(run => run.Id != runId)) {
            return Task.CompletedTask;
        }
        manualRevision++;
        DesiredRunId = runId;
        Detail = null;
        return ReadDetailAsync();
    }

    private async Task ReadCatalogAndRunsAsync() {
        var manual = manualRevision;
        var request = await ReadAsync(catalog, hasCatalog, reads.ReadAgentsAsync, agents => {
            if (agents.Any(agent => agent.Id == Guid.Empty)
                || agents.Select(agent => agent.Id).Distinct().Count() != agents.Count) {
                throw new InvalidDataException("Agent catalog identity validation failed.");
            }
            Agents = agents.ToImmutableArray();
            hasCatalog = true;
            ResolveAgent();
        });
        if (request is not null && IsCurrent(catalog, request) && AgentResolved) {
            await ReadRunsAsync(manual);
        }
    }

    private void ResolveAgent() {
        AcceptedAgent = DesiredAgentId is { } id ? Agents.FirstOrDefault(agent => agent.Id == id) : null;
        AgentResolved = DesiredAgentId != Guid.Empty && (DesiredAgentId is null || AcceptedAgent is not null);
        AcceptedAgentId = AgentResolved ? DesiredAgentId : null;
        catalog.State = AgentResolved ? GovernanceLaneState.Ready : new(GovernanceReadPhase.Unavailable,
            "The requested technical agent is unavailable. Retry its catalog lookup.");
        if (AgentResolved) {
            return;
        }
        Reset(list);
        Reset(detail);
        Runs = [];
        Detail = null;
        hasList = false;
        AcceptedListAgentId = null;
    }

    private async Task ReadRunsAsync(long selectionAtStart) {
        var agentId = DesiredAgentId;
        var request = await ReadAsync(list, hasList,
            token => reads.ReadRunsAsync(agentId, token), rows => {
                if (rows.Any(row => row.Id == Guid.Empty || agentId.HasValue && row.AgentId != agentId.Value)
                    || rows.Select(row => row.Id).Distinct().Count() != rows.Count) {
                    throw new InvalidDataException("Run list identity validation failed.");
                }
                Runs = rows.ToImmutableArray();
                hasList = true;
                AcceptedListAgentId = agentId;
                DesiredRunId ??= Runs.Length == 0 ? null : Runs[0].Id;
                if (DesiredRunId.HasValue && Runs.All(row => row.Id != DesiredRunId.Value)) {
                    Reset(detail);
                    Detail = null;
                    detail.State = new(GovernanceReadPhase.Unavailable,
                        "The selected execution run is no longer in this result. Choose another run or refresh.");
                } else if (DesiredRunId is null) {
                    Reset(detail);
                    Detail = null;
                } else if (detail.State.Phase == GovernanceReadPhase.Unavailable) {
                    detail.State = GovernanceLaneState.Idle;
                }
            });
        if (request is null || !IsCurrent(list, request) || selectionAtStart != manualRevision
            || DesiredRunId is null || detail.State.Phase == GovernanceReadPhase.Unavailable) {
            return;
        }
        await ReadDetailAsync();
    }

    private Task ReadDetailAsync() {
        var row = Runs.FirstOrDefault(run => run.Id == DesiredRunId);
        if (row is null) {
            return Task.CompletedTask;
        }
        return ReadAsync(detail, Detail?.Run.Id == row.Id,
            token => reads.ReadDetailAsync(row.Id, token), loaded => {
                if (loaded.Run.Id != row.Id || loaded.Run.AgentId != row.AgentId) {
                    throw new InvalidDataException("Run detail identity validation failed.");
                }
                Detail = loaded with {
                    ExecutionLog = loaded.ExecutionLog.ToImmutableArray(),
                    Metrics = loaded.Metrics.ToImmutableArray(),
                    Approvals = loaded.Approvals.ToImmutableArray(),
                    Artifacts = loaded.Artifacts.ToImmutableArray(),
                    Checkpoints = loaded.Checkpoints.ToImmutableArray(),
                    ToolReceipts = loaded.ToolReceipts.ToImmutableArray(),
                    UsageObservations = loaded.UsageObservations.ToImmutableArray()
                };
            }, row.Id);
    }

    private async Task<ReadRequest?> ReadAsync<T>(ReadLane lane, bool retained,
        Func<CancellationToken, Task<T>> read, Action<T> accept, Guid? runId = null) {
        Cancel(lane);
        var request = new ReadRequest(TargetRevision, DesiredAgentId, runId);
        lane.Current = request;
        lane.State = new(retained ? GovernanceReadPhase.Refreshing : GovernanceReadPhase.Loading);
        var accepted = false;
        try {
            await NotifyAsync();
            if (!IsCurrent(lane, request)) {
                return null;
            }
            try {
                var result = await read(request.Token);
                if (IsCurrent(lane, request)) {
                    lane.State = GovernanceLaneState.Ready;
                    accept(result);
                    accepted = true;
                }
            } catch (OperationCanceledException) when (request.Token.IsCancellationRequested) {
            } catch (Exception exception) {
                if (IsCurrent(lane, request)) {
                    lane.State = new(retained ? GovernanceReadPhase.Stale : GovernanceReadPhase.Failed, lane.Kind switch {
                        ReadKind.Catalog => "Technical agents could not be loaded. Retry the catalog.",
                        ReadKind.List => "Execution runs could not be refreshed. Retry the run list.",
                        ReadKind.Detail => "Execution details could not be loaded. Retry this run.",
                        _ => throw new InvalidOperationException("Unknown Governance read lane.")
                    });
                    logger.LogWarning("Governance {Lane} read failed ({FailureType}); target {AgentId}, run {RunId}.",
                        lane.Kind, exception.GetType().Name, request.AgentId, request.RunId);
                }
            }
        } finally {
            if (IsCurrent(lane, request)) {
                request.Pending = false;
            }
            request.Dispose();
        }
        if (IsCurrent(lane, request)) {
            await NotifyAsync();
        }
        return accepted && IsCurrent(lane, request) ? request : null;
    }

    private bool IsCurrent(ReadLane lane, ReadRequest request)
        => !disposed && ReferenceEquals(lane.Current, request) && TargetRevision == request.TargetRevision
            && DesiredAgentId == request.AgentId && !request.Token.IsCancellationRequested
            && (lane.Kind != ReadKind.Detail || DesiredRunId == request.RunId);

    private Task NotifyAsync() => disposed || changed is null ? Task.CompletedTask : changed();

    private static void Cancel(ReadLane lane) {
        var request = lane.Current;
        lane.Current = null;
        request?.Cancel();
    }

    private static void Reset(ReadLane lane) {
        Cancel(lane);
        lane.State = GovernanceLaneState.Idle;
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Cancel(catalog);
        Cancel(list);
        Cancel(detail);
    }

    private enum ReadKind { Catalog, List, Detail }

    private sealed class ReadLane(ReadKind kind) {
        public ReadKind Kind { get; } = kind;
        public ReadRequest? Current { get; set; }
        public GovernanceLaneState State { get; set; } = GovernanceLaneState.Idle;
        public bool Pending => Current?.Pending == true;
    }

    private sealed class ReadRequest : IDisposable {
        private readonly CancellationTokenSource source = new();
        private bool disposed;
        public long TargetRevision { get; }
        public Guid? AgentId { get; }
        public Guid? RunId { get; }
        public CancellationToken Token { get; }
        public bool Pending { get; set; } = true;

        public ReadRequest(long targetRevision, Guid? agentId, Guid? runId) {
            TargetRevision = targetRevision;
            AgentId = agentId;
            RunId = runId;
            Token = source.Token;
        }

        public void Cancel() {
            if (disposed) {
                return;
            }
            try {
                source.Cancel();
            } finally {
                Dispose();
            }
        }

        public void Dispose() {
            if (disposed) {
                return;
            }
            disposed = true;
            source.Dispose();
        }
    }
}
