using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public sealed class AgentsOverviewSession(
    IAgentsWorkspaceQuery query,
    ILogger<AgentsOverviewSession> logger,
    Func<Task>? changed = null, TimeProvider? clock = null) : IDisposable {
    private readonly ReadLane header = new(ReadKind.Header);
    private readonly ReadLane overview = new(ReadKind.Overview);
    private readonly ReadLane usage = new(ReadKind.Usage);
    private (ProviderUsageWorkloadSelection Selection, ProviderUsagePeriod Period)? requestedUsage;
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private bool aggregatesDemanded;
    private bool disposed;

    public AgentsHeaderSnapshot? Header { get; private set; }
    public AgentRuntimeSummary? Overview { get; private set; }
    public ProviderUsageSnapshot? AcceptedUsage { get; private set; }
    public bool HeaderLoading => header.Current?.Pending == true;
    public bool OverviewLoading => overview.Current?.Pending == true;
    public bool UsageLoading => usage.Current?.Pending == true;
    public string? HeaderError => header.Error;
    public string? OverviewError => overview.Error;
    public string? UsageError => usage.Error;

    public ProviderUsageSnapshot? GetAcceptedUsage(ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period = ProviderUsagePeriod.SevenDays)
        => AcceptedUsage?.Query is { } accepted && accepted.Selection == selection && accepted.Period == period ? AcceptedUsage : null;

    public Task EnsureAsync(AgentWorkspaceSection section, ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period = ProviderUsagePeriod.SevenDays) {
        SetDemand(section, selection, period);
        if (disposed) {
            return Task.CompletedTask;
        }
        var headerTask = ReadHeaderAsync(retry: false);
        return aggregatesDemanded
            ? Task.WhenAll(headerTask, ReadOverviewAsync(retry: false), ReadUsageAsync(selection, period, retry: false))
            : headerTask;
    }

    public Task RefreshDemandedAsync(AgentWorkspaceSection section, ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period = ProviderUsagePeriod.SevenDays) {
        SetDemand(section, selection, period);
        if (disposed) {
            return Task.CompletedTask;
        }
        var headerTask = ReadHeaderAsync(retry: true);
        return aggregatesDemanded
            ? Task.WhenAll(headerTask, ReadOverviewAsync(retry: true), ReadUsageAsync(selection, period, retry: true))
            : headerTask;
    }

    public Task RetryHeaderAsync() => disposed ? Task.CompletedTask : ReadHeaderAsync(retry: true);

    public Task RetryOverviewAsync()
        => disposed || !aggregatesDemanded ? Task.CompletedTask : ReadOverviewAsync(retry: true);

    public Task RetryUsageAsync(ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period = ProviderUsagePeriod.SevenDays)
        => disposed || !aggregatesDemanded || (selection, period) != requestedUsage
            ? Task.CompletedTask : ReadUsageAsync(selection, period, retry: true);

    private void SetDemand(AgentWorkspaceSection section, ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period = ProviderUsagePeriod.SevenDays) {
        if (disposed) {
            return;
        }
        if (!Enum.IsDefined(section)) {
            throw new ArgumentOutOfRangeException(nameof(section));
        }
        if (selection is not (ProviderUsageWorkloadSelection.Agents or ProviderUsageWorkloadSelection.SimpleChats or ProviderUsageWorkloadSelection.Both)) {
            throw new ArgumentOutOfRangeException(nameof(selection));
        }
        if (!Enum.IsDefined(period)) {
            throw new ArgumentOutOfRangeException(nameof(period));
        }
        if (requestedUsage != (selection, period)) {
            CancelPending(usage);
            requestedUsage = (selection, period);
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
                TeamShortcuts = snapshot.TeamShortcuts.ToImmutableArray()
            };
        });

    private Task ReadUsageAsync(ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period, bool retry) {
        if (!retry && usage.Current is { Query: { } existing } current && existing.Selection == selection && existing.Period == period) {
            return current.Task;
        }
        var request = new ProviderUsageQuery(selection, period, clock.GetUtcNow());
        return StartAsync(usage, request, retry, token => query.ReadUsageAsync(request, token).AsTask(), snapshot => {
            if (snapshot.Query != request || snapshot.Selection != selection) {
                throw new InvalidDataException("Usage returned a different query than requested.");
            }
            AcceptedUsage = snapshot with {
                Consumers = snapshot.Consumers.ToImmutableArray(),
                Providers = snapshot.Providers.ToImmutableArray(),
                Models = snapshot.Models.ToImmutableArray(),
                Sources = snapshot.Sources.ToImmutableArray()
            };
        });
    }

    private Task StartAsync<T>(ReadLane lane, ProviderUsageQuery? selection, bool retry,
        Func<CancellationToken, Task<T>> read, Action<T> accept) {
        if (!retry && lane.Current is { } existing && existing.Query == selection) {
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
                        ReadKind.Usage => "Usage evidence could not be refreshed. Retry the selected workload and period.",
                        _ => throw new InvalidOperationException("Unknown read lane.")
                    };
                    logger.LogWarning("Agents {Lane} read {Generation} failed ({FailureType}); requested usage scope {Scope}.",
                        lane.Kind, request.Generation, exception.GetType().Name, request.Query);
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
            && (lane.Kind != ReadKind.Usage || request.Query is { } identity && requestedUsage == (identity.Selection, identity.Period));

    private static void CancelPending(ReadLane lane) {
        if (lane.Current is not { Pending: true } request) {
            return;
        }
        lane.Generation++;
        lane.Current = null;
        request.Cancel();
    }

    public void InvalidateContext() {
        Dispose();
        Header = null;
        Overview = null;
        AcceptedUsage = null;
        usage.Error = "The workspace changed. Reload to read usage for the current workspace.";
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
        public ProviderUsageQuery? Query { get; }
        public CancellationToken Token { get; }
        public bool Pending { get; set; } = true;
        public Task Task { get; set; } = System.Threading.Tasks.Task.CompletedTask;

        public ReadRequest(long generation, ProviderUsageQuery? selection) {
            Generation = generation;
            Query = selection;
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
