using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Diagnostics;
using Microsoft.Extensions.Logging;
using static CanDoItAll.AgentFramework.UI.Governance.GovernancePresentationMapping;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class AgentDiagnosticsSession(IAgentDiagnosticsReads reads, ILogger<AgentDiagnosticsSession> logger) : IDisposable {
    private readonly Dictionary<DiagnosticsLane, CancellationTokenSource> active = [];
    private ImmutableDictionary<Guid, string> agents = ImmutableDictionary<Guid, string>.Empty;
    private long revision;
    private bool disposed;
    public DiagnosticsPresentation Presentation { get; private set; } = DiagnosticsPresentation.Initial;
    public event Action? Changed;

    public Task RefreshAsync() => StartAsync(Enum.GetValues<DiagnosticsLane>());
    public Task RetryAsync(DiagnosticsLane lane) => StartAsync([lane]);

    private Task StartAsync(DiagnosticsLane[] lanes) {
        ObjectDisposedException.ThrowIf(disposed, this);
        var requested = ++revision;
        var requests = lanes.Select(lane => (Lane: lane, Owner: new CancellationTokenSource())).ToArray();
        var previous = lanes.Select(lane => active.GetValueOrDefault(lane)).ToArray();
        foreach (var (lane, owner) in requests) {
            active[lane] = owner;
            var old = Presentation.ReadState(lane);
            SetState(lane, old with { DesiredRevision = requested,
                Phase = old.AcceptedRevision.HasValue ? DiagnosticsReadPhase.Refreshing : DiagnosticsReadPhase.Loading });
        }
        foreach (var owner in previous) {
            owner?.Cancel();
        }
        Changed?.Invoke();
        return Task.WhenAll(requests.Select(request => ReadAsync(request.Lane, request.Owner, requested)));
    }

    private async Task ReadAsync(DiagnosticsLane lane, CancellationTokenSource owner, long requested) {
        try {
            if (!IsCurrent(lane, owner)) {
                return;
            }
            switch (lane) {
                case DiagnosticsLane.Dashboard:
                    var dashboard = await reads.ReadDashboardAsync(owner.Token);
                    if (!IsCurrent(lane, owner)) {
                        return;
                    }
                    Presentation = Presentation with { Dashboard = DiagnosticsPresentationMapping.Dashboard(dashboard) };
                    break;
                case DiagnosticsLane.Agents:
                    var catalog = await reads.ReadAgentsAsync(owner.Token);
                    if (!IsCurrent(lane, owner)) {
                        return;
                    }
                    agents = catalog.GroupBy(agent => agent.Id).ToImmutableDictionary(group => group.Key, group => Text(group.First().Name));
                    break;
                case DiagnosticsLane.Runs:
                    var runs = await reads.ReadRecentRunsAsync(owner.Token);
                    if (!IsCurrent(lane, owner)) {
                        return;
                    }
                    Presentation = Presentation with { Runs = runs.Take(12).Select(DiagnosticsPresentationMapping.Run).ToImmutableArray() };
                    break;
            }
            Presentation = Presentation with { Runs = Presentation.Runs.Select(run => run with {
                Agent = agents.GetValueOrDefault(run.AgentId, "Unknown agent") }).ToImmutableArray() };
            SetState(lane, new(DiagnosticsReadPhase.Ready, requested, requested));
        } catch (OperationCanceledException) when (owner.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent(lane, owner)) {
                logger.LogWarning("Diagnostics {Lane} revision {Revision} failed ({ExceptionType}).", lane, requested, exception.GetType().Name);
                var state = Presentation.ReadState(lane);
                SetState(lane, state with { Phase = state.AcceptedRevision.HasValue ? DiagnosticsReadPhase.Stale : DiagnosticsReadPhase.Failed });
            }
        } finally {
            if (IsCurrent(lane, owner)) {
                active.Remove(lane);
                Changed?.Invoke();
            }
            owner.Dispose();
        }
    }

    private bool IsCurrent(DiagnosticsLane lane, CancellationTokenSource owner)
        => !disposed && ReferenceEquals(active.GetValueOrDefault(lane), owner) && !owner.IsCancellationRequested;

    private void SetState(DiagnosticsLane lane, DiagnosticsReadState state) {
        Presentation = lane switch {
            DiagnosticsLane.Dashboard => Presentation with { DashboardState = state },
            DiagnosticsLane.Agents => Presentation with { AgentsState = state },
            _ => Presentation with { RunsState = state }
        };
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Changed = null;
        var owners = active.Values.ToArray();
        active.Clear();
        foreach (var owner in owners) {
            owner.Cancel();
        }
    }
}
