using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public sealed class AgentsOverviewSession(
    IAgentsWorkspaceQuery query,
    ILogger<AgentsOverviewSession> logger,
    Func<Task>? changed = null) : IDisposable {
    private readonly ReadLane header = new(ReadKind.Header);
    private readonly ReadLane overview = new(ReadKind.Overview);
    private readonly ReadLane usage = new(ReadKind.Usage);
    private ProviderUsageWorkloadSelection? requestedUsage;
    private bool aggregatesDemanded;
    private bool disposed;

    public AgentsHeaderSnapshot? Header { get; private set; }
    public AgentOverviewSnapshot? Overview { get; private set; }
    public ProviderUsageSnapshot? AcceptedUsage { get; private set; }
    public bool HeaderLoading => header.Current?.Pending == true;
    public bool OverviewLoading => overview.Current?.Pending == true;
    public bool UsageLoading => usage.Current?.Pending == true;
    public string? HeaderError => header.Error;
    public string? OverviewError => overview.Error;
    public string? UsageError => usage.Error;

    public ProviderUsageSnapshot? GetAcceptedUsage(ProviderUsageWorkloadSelection selection)
        => AcceptedUsage?.Selection == selection ? AcceptedUsage : null;

    public Task EnsureAsync(AgentWorkspaceSection section, ProviderUsageWorkloadSelection selection) {
        SetDemand(section, selection);
        if (disposed) {
            return Task.CompletedTask;
        }
        var headerTask = ReadHeaderAsync(retry: false);
        return aggregatesDemanded
            ? Task.WhenAll(headerTask, ReadOverviewAsync(retry: false), ReadUsageAsync(selection, retry: false))
            : headerTask;
    }

    public Task RefreshDemandedAsync(AgentWorkspaceSection section, ProviderUsageWorkloadSelection selection) {
        SetDemand(section, selection);
        if (disposed) {
            return Task.CompletedTask;
        }
        var headerTask = ReadHeaderAsync(retry: true);
        return aggregatesDemanded
            ? Task.WhenAll(headerTask, ReadOverviewAsync(retry: true), ReadUsageAsync(selection, retry: true))
            : headerTask;
    }

    public Task RetryHeaderAsync() => disposed ? Task.CompletedTask : ReadHeaderAsync(retry: true);

    public Task RetryOverviewAsync()
        => disposed || !aggregatesDemanded ? Task.CompletedTask : ReadOverviewAsync(retry: true);

    public Task RetryUsageAsync(ProviderUsageWorkloadSelection selection)
        => disposed || !aggregatesDemanded || selection != requestedUsage
            ? Task.CompletedTask : ReadUsageAsync(selection, retry: true);

    private void SetDemand(AgentWorkspaceSection section, ProviderUsageWorkloadSelection selection) {
        if (disposed) {
            return;
        }
        if (!Enum.IsDefined(section)) {
            throw new ArgumentOutOfRangeException(nameof(section));
        }
        if (selection is not (ProviderUsageWorkloadSelection.Agents or ProviderUsageWorkloadSelection.SimpleChats or ProviderUsageWorkloadSelection.Both)) {
            throw new ArgumentOutOfRangeException(nameof(selection));
        }
        if (requestedUsage != selection) {
            CancelPending(usage);
            requestedUsage = selection;
        }
        aggregatesDemanded = !section.IsHistoryHost();
        if (!aggregatesDemanded) {
            CancelPending(overview);
            CancelPending(usage);
        }
    }

    private Task ReadHeaderAsync(bool retry)
        => StartAsync(header, null, retry, query.ReadHeaderAsync, snapshot => {
            Header = snapshot with {
                HrAgent = snapshot.Failures.HasFlag(AgentsHeaderFailure.HrAgent) ? Header?.HrAgent : snapshot.HrAgent,
                AvatarImageUrls = snapshot.Failures.HasFlag(AgentsHeaderFailure.Avatars) && Header is { } prior
                    ? prior.AvatarImageUrls
                    : snapshot.AvatarImageUrls.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
                BoundResourceCount = snapshot.Failures.HasFlag(AgentsHeaderFailure.BoundResources)
                    ? Header?.BoundResourceCount : snapshot.BoundResourceCount
            };
        });

    private Task ReadOverviewAsync(bool retry)
        => StartAsync(overview, null, retry, query.ReadOverviewAsync, snapshot => {
            Overview = snapshot with {
                TopAgents = snapshot.TopAgents.ToImmutableArray(),
                TopFailingAgents = snapshot.TopFailingAgents.ToImmutableArray(),
                ProviderUsage = snapshot.ProviderUsage.ToImmutableArray(),
                ModelUsage = snapshot.ModelUsage.ToImmutableArray(),
                TeamShortcuts = snapshot.TeamShortcuts.ToImmutableArray()
            };
        });

    private Task ReadUsageAsync(ProviderUsageWorkloadSelection selection, bool retry)
        => StartAsync(usage, selection, retry, token => query.ReadUsageAsync(selection, token).AsTask(), snapshot => {
            if (snapshot.Selection != selection) {
                throw new InvalidDataException("Usage returned a different scope than requested.");
            }
            AcceptedUsage = snapshot with {
                Consumers = snapshot.Consumers.ToImmutableArray(),
                Providers = snapshot.Providers.ToImmutableArray(),
                Models = snapshot.Models.ToImmutableArray(),
                Sources = snapshot.Sources.ToImmutableArray()
            };
        });

    private Task StartAsync<T>(ReadLane lane, ProviderUsageWorkloadSelection? selection, bool retry,
        Func<CancellationToken, Task<T>> read, Action<T> accept) {
        if (!retry && lane.Current is { } existing && existing.Selection == selection) {
            return existing.Task;
        }
        lane.Current?.Cancel();
        var request = new ReadRequest(++lane.Generation, selection);
        lane.Current = request;
        lane.Error = null;
        request.Task = CompleteAsync();
        return request.Task;

        async Task CompleteAsync() {
            try {
                var result = await read(request.Token);
                if (IsCurrent(lane, request)) {
                    accept(result);
                }
            } catch (OperationCanceledException) when (request.Token.IsCancellationRequested) {
            } catch (Exception exception) {
                if (IsCurrent(lane, request)) {
                    lane.Error = lane.Kind switch {
                        ReadKind.Header => "Header information could not be refreshed. Retry the header read.",
                        ReadKind.Overview => "The agent runtime summary could not be refreshed. Retry the Overview read.",
                        ReadKind.Usage => "Usage evidence could not be refreshed. Retry the selected scope.",
                        _ => throw new InvalidOperationException("Unknown read lane.")
                    };
                    logger.LogWarning("Agents {Lane} read {Generation} failed ({FailureType}); requested usage scope {Scope}.",
                        lane.Kind, request.Generation, exception.GetType().Name, request.Selection);
                }
            } finally {
                if (IsCurrent(lane, request)) {
                    request.Pending = false;
                }
                request.Dispose();
            }
            if (IsCurrent(lane, request) && changed is not null) {
                await changed();
            }
        }
    }

    private bool IsCurrent(ReadLane lane, ReadRequest request)
        => !disposed && lane.Generation == request.Generation && ReferenceEquals(lane.Current, request)
            && !request.Token.IsCancellationRequested
            && (lane.Kind == ReadKind.Header || aggregatesDemanded)
            && (lane.Kind != ReadKind.Usage || requestedUsage == request.Selection);

    private static void CancelPending(ReadLane lane) {
        if (lane.Current is not { Pending: true } request) {
            return;
        }
        lane.Generation++;
        lane.Current = null;
        request.Cancel();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        CancelPending(header);
        CancelPending(overview);
        CancelPending(usage);
    }

    private enum ReadKind { Header, Overview, Usage }

    private sealed class ReadLane(ReadKind kind) {
        public ReadKind Kind { get; } = kind;
        public long Generation { get; set; }
        public ReadRequest? Current { get; set; }
        public string? Error { get; set; }
    }

    private sealed class ReadRequest : IDisposable {
        private readonly CancellationTokenSource cancellation = new();
        private bool disposed;
        public long Generation { get; }
        public ProviderUsageWorkloadSelection? Selection { get; }
        public CancellationToken Token { get; }
        public bool Pending { get; set; } = true;
        public Task Task { get; set; } = System.Threading.Tasks.Task.CompletedTask;

        public ReadRequest(long generation, ProviderUsageWorkloadSelection? selection) {
            Generation = generation;
            Selection = selection;
            Token = cancellation.Token;
        }

        public void Cancel() {
            if (disposed) {
                return;
            }
            cancellation.Cancel();
        }

        public void Dispose() {
            if (disposed) {
                return;
            }
            disposed = true;
            cancellation.Dispose();
        }
    }
}
