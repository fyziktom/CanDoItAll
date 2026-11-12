using System.Collections.Immutable;
using System.Text;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum HistoryScenario {
    Normal, Large, Empty, Partial, Failure, Denied, MetadataDenied, ContentDenied,
    Canonical, Expired, Pending, Redacted, Unavailable, DelayedSearch, DelayedMetadata, DelayedContent, TwoWorkspaces
}

public enum HistoryReadLayer { Search, Metadata, Content }

public sealed class HistoryScenarioReader(HistoryScenario scenario, TimeSpan? automaticCompletion = null) : IProviderRequestHistory, IDisposable {
    public static ProviderIdentity Provider { get; } = new(Guid.Parse("29106dba-fdca-42a3-8cdb-430000000010"));
    public static TimeProvider Clock { get; } = new ScenarioClock();
    private readonly ImmutableArray<HistoryEntry> entries = CreateEntries(scenario == HistoryScenario.Large ? 240 : 12);
    private readonly Dictionary<string, (ProviderRequestHistoryQuery Query, int Offset)> cursors = [];
    private readonly List<TaskCompletionSource<bool>> pending = [];
    private int canceledReads;
    private bool disposed;

    public event Action? Changed;
    public List<ProviderRequestHistoryQuery> Queries { get; } = [];
    public List<HistoryEntryId> MetadataReads { get; } = [];
    public List<(HistoryEntryId Entry, CanonicalEvidenceReference? Owner)> ContentReads { get; } = [];
    public int PendingReads => pending.Count;
    public int CanceledReads => canceledReads;

    public async Task<HistoryPage> SearchAsync(ProviderRequestHistoryQuery query, CancellationToken cancellationToken) {
        Queries.Add(query);
        Changed?.Invoke();
        await DelayAsync(HistoryReadLayer.Search, cancellationToken);
        RequireAvailable();
        if (scenario is HistoryScenario.Denied or HistoryScenario.Failure) {
            throw new ProviderHistoryException(scenario == HistoryScenario.Denied ? HistoryFailure.Denied : HistoryFailure.Unavailable,
                "Synthetic search refusal.");
        }
        if (query.PageSize is < 1 or > 200 || query.ToUtc <= query.FromUtc || query.ToUtc - query.FromUtc > TimeSpan.FromDays(31)) {
            throw new ProviderHistoryException(HistoryFailure.InvalidQuery, "Synthetic query boundary.");
        }
        var binding = query with { Cursor = null };
        var offset = 0;
        if (query.Cursor is { } cursor) {
            if (!cursors.TryGetValue(cursor, out var position) || position.Query != binding) {
                throw new ProviderHistoryException(HistoryFailure.InvalidCursor, "Synthetic cursor binding refused.");
            }
            offset = position.Offset;
        }
        var matches = entries.Where(entry => Matches(entry, query));
        var rows = scenario == HistoryScenario.Empty ? [] : matches.Skip(offset).Take(query.PageSize + 1).ToArray();
        string? next = null;
        if (rows.Length > query.PageSize) {
            next = Guid.NewGuid().ToString("N");
            cursors.Add(next, (binding, offset + query.PageSize));
        }
        var coverage = scenario == HistoryScenario.Partial ? HistoryCoverageState.Partial : HistoryCoverageState.Current;
        return new(rows.Take(query.PageSize).Select(Describe).ToImmutableArray(), next,
            new(coverage, HistorySandboxFixture.Now.AddMinutes(-1)), HistorySandboxFixture.Now);
    }

    public async Task<HistoryMetadata?> GetMetadataAsync(HistoryEntryId entryId, CancellationToken cancellationToken) {
        MetadataReads.Add(entryId);
        Changed?.Invoke();
        await DelayAsync(HistoryReadLayer.Metadata, cancellationToken);
        RequireAvailable();
        if (scenario == HistoryScenario.MetadataDenied) {
            throw new ProviderHistoryException(HistoryFailure.Denied, "Synthetic metadata refusal.");
        }
        var entry = entries.SingleOrDefault(item => item.Id == entryId);
        return entry is null ? null : new(Describe(entry), Owners(entry));
    }

    public async Task<HistoryDetail> GetDetailAsync(HistoryEntryId entryId, CanonicalEvidenceReference? owner, CancellationToken cancellationToken) {
        ContentReads.Add((entryId, owner));
        Changed?.Invoke();
        await DelayAsync(HistoryReadLayer.Content, cancellationToken);
        RequireAvailable();
        if (scenario == HistoryScenario.ContentDenied) {
            throw new ProviderHistoryException(HistoryFailure.Denied, "Synthetic content refusal.");
        }
        var entry = entries.SingleOrDefault(item => item.Id == entryId);
        if (entry is null || (scenario == HistoryScenario.Canonical
            ? !Owners(entry).Any(link => link.Source == owner && link.CanReadContent) : owner is not null)) {
            return new(entryId, HistoryDetailState.Unavailable);
        }
        var state = Describe(entry).DetailState;
        if (state is HistoryDetailState.Expired or HistoryDetailState.PendingCanonical or HistoryDetailState.Unavailable) {
            return new(entryId, state);
        }
        var input = scenario == HistoryScenario.Redacted ? "Synthetic [redacted] input" : HistorySandboxFixture.SyntheticInput;
        var response = scenario == HistoryScenario.Large
            ? string.Join('\n', Enumerable.Repeat(HistorySandboxFixture.SyntheticResponse + " 界", 140))
            : HistorySandboxFixture.SyntheticResponse;
        var flags = scenario == HistoryScenario.Redacted
            ? HistoryDetailFlags.Redacted | HistoryDetailFlags.Truncated : HistoryDetailFlags.PriorContextNotCaptured;
        var originalInput = scenario == HistoryScenario.Redacted ? input + " omitted synthetic characters" : input;
        var section = new HistoryCapturedText(input, Encoding.UTF8.GetByteCount(originalInput), Encoding.UTF8.GetByteCount(input), flags);
        return new(entryId, state, owner is null ? section : null,
            new(response, Encoding.UTF8.GetByteCount(response), Encoding.UTF8.GetByteCount(response), HistoryDetailFlags.None),
            HistorySandboxFixture.Now.AddDays(7)) {
            Sections = owner is null ? [] : [new($"Synthetic {owner.Kind} context", section)]
        };
    }

    public void CompletePending(bool fail = false) {
        foreach (var completion in pending.ToArray()) {
            completion.TrySetResult(fail);
        }
    }

    private async Task DelayAsync(HistoryReadLayer layer, CancellationToken cancellationToken) {
        var delayed = (scenario, layer) is (HistoryScenario.DelayedSearch, HistoryReadLayer.Search)
            or (HistoryScenario.DelayedMetadata, HistoryReadLayer.Metadata)
            or (HistoryScenario.DelayedContent, HistoryReadLayer.Content);
        if (!delayed) {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }
        if (pending.Count == 8) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, "Complete the pending synthetic reads first.");
        }
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending.Add(completion);
        using var registration = cancellationToken.Register(() => {
            Interlocked.Increment(ref canceledReads);
            Changed?.Invoke();
        });
        Changed?.Invoke();
        try {
            if (automaticCompletion is { } delay && await Task.WhenAny(completion.Task, Task.Delay(delay)) != completion.Task) {
                completion.TrySetResult(false);
            }
            // Deliberately finish after cancellation so the real UI must fence late success and failure.
            if (await completion.Task) {
                throw new ProviderHistoryException(HistoryFailure.Unavailable, "Synthetic delayed failure.");
            }
        } finally {
            pending.Remove(completion);
            Changed?.Invoke();
        }
    }

    private void RequireAvailable() => ObjectDisposedException.ThrowIf(disposed, this);

    private HistoryEntry Describe(HistoryEntry entry) => entry with {
        MetadataAuthority = scenario == HistoryScenario.Canonical ? HistoryMetadataAuthority.CanonicalProjection : HistoryMetadataAuthority.Standalone,
        RetentionAuthority = scenario == HistoryScenario.Canonical ? HistoryRetentionAuthority.CanonicalOwner : HistoryRetentionAuthority.HistoryPolicy,
        DetailState = scenario switch {
            HistoryScenario.Expired => HistoryDetailState.Expired,
            HistoryScenario.Pending => HistoryDetailState.PendingCanonical,
            HistoryScenario.Unavailable => HistoryDetailState.Unavailable,
            HistoryScenario.Canonical => HistoryDetailState.Canonical,
            _ => HistoryDetailState.Captured
        }
    };

    private ImmutableArray<HistoryOwnerLink> Owners(HistoryEntry entry) => scenario == HistoryScenario.Canonical
        ? [CreateOwner(entry, HistorySourceKind.AgentConversation), CreateOwner(entry, HistorySourceKind.SimpleChat), CreateOwner(entry, HistorySourceKind.Workflow)] : [];

    private static HistoryOwnerLink CreateOwner(HistoryEntry entry, HistorySourceKind kind) => new(entry.Id,
        new(entry.Partition, kind, new("synthetic-owner"), new("synthetic-evidence")), new(1),
        kind == HistorySourceKind.Workflow ? HistoryOwnerRole.PrimaryEvidence : HistoryOwnerRole.ContentOwner, HistoryOwnerState.Linked);

    private static bool Matches(HistoryEntry entry, ProviderRequestHistoryQuery query) =>
        (query.Scope is HistoryProviderScope.AllAuthorized || query.Scope is HistoryProviderScope.SingleProvider provider && entry.Provider.Id == provider.Provider)
        && entry.SortAtUtc >= query.FromUtc && entry.SortAtUtc < query.ToUtc
        && (query.Model is null || query.Model == entry.Provider.ResolvedModel || query.Model == entry.Provider.RequestedModel)
        && (query.Workload is null || query.Workload == entry.Workload)
        && (query.Operation is null || query.Operation == entry.Operation)
        && (query.Outcome is null || query.Outcome == entry.Outcome)
        && (query.PriceState is null || query.PriceState == entry.Price.State)
        && (query.CredentialId is null || query.CredentialId == entry.Caller.CredentialId)
        && (query.Subject is null || query.Subject == entry.Caller.Subject)
        && (query.Issuer is null || query.Issuer == entry.Caller.Issuer)
        && (query.RequestId is null || query.RequestId == entry.RequestId)
        && (query.AttemptId is null || query.AttemptId == entry.AttemptId)
        && (query.CorrelationId is null || query.CorrelationId == entry.CorrelationId)
        && (query.ExternalReference is null || query.ExternalReference == entry.ExternalReference);

    private static ImmutableArray<HistoryEntry> CreateEntries(int count) => Enumerable.Range(0, count)
        .Select(index => HistorySandboxFixture.Entry with {
            Id = HistoryEntryId.New(), RequestId = ProviderRequestId.New(), AttemptId = ProviderAttemptId.New(),
            SortAtUtc = HistorySandboxFixture.Now.AddMinutes(-index - 1),
            StartedAtUtc = HistorySandboxFixture.Now.AddMinutes(-index - 1),
            FinishedAtUtc = HistorySandboxFixture.Now.AddMinutes(-index - 1).AddMilliseconds(250),
            Provider = HistorySandboxFixture.Entry.Provider with { Id = Provider },
            Caller = new(HistoryAuthenticationKind.ManagedCredential, new(Guid.Parse("29106dba-fdca-42a3-8cdb-430000000020")),
                "synthetic-issuer", index % 2 == 0 ? "synthetic-client-a" : "synthetic-client-b"),
            Usage = new(HistoryUsageState.Partial, 12, 8, CachedInputTokens: 0),
            Price = new(HistoryPriceState.ExplicitFree, 0, "USD"),
            ExternalReference = new("synthetic-project", "erp.company-project"), CorrelationId = "synthetic-correlation"
        }).ToImmutableArray();

    public void Dispose() {
        disposed = true;
        Changed = null;
        CompletePending();
        cursors.Clear();
    }

    private sealed class ScenarioClock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => HistorySandboxFixture.Now;
    }
}
