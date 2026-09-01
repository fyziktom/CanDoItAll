using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

internal sealed class ProviderHistoryQueryTestContext : IDisposable {
    internal static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    internal static readonly ProviderIdentity Provider = new(Guid.Parse("76100000-0000-0000-0000-000000000001"));
    internal static readonly HistoryPartition Partition = new(Guid.NewGuid(), Guid.NewGuid(), "test");
    internal Access Authority { get; } = new();
    internal Reader Store { get; } = new();
    internal Source Owner { get; } = new();
    internal HistoryReadConcurrency Concurrency { get; } = new();
    internal HistoryCursorProtector Cursors { get; } = new(new EphemeralDataProtectionProvider());
    internal ProviderRequestHistoryService Service { get; }
    internal ProviderRequestHistoryQuery Query { get; } = new(new HistoryProviderScope.AllAuthorized(), Now.AddHours(-1), Now.AddHours(1)) { PageSize = 1 };

    internal ProviderHistoryQueryTestContext() {
        Service = new(Authority, Store, Cursors, [Owner],
            new(Authority, Concurrency, TimeProvider.System, NullLogger<HistoryAuthorizedOperation>.Instance), TimeProvider.System);
    }

    internal static HistoryEntry Entry() => new(HistoryEntryId.New(), Partition, ProviderRequestId.New(), ProviderAttemptId.New(),
        HistoryGranularity.ProviderCallAttempt, Now, HistoryTimeBasis.AttemptStarted, Now, Now,
        new(Provider, "Provider", "OpenAI", new("public-model"), new("resolved-model")),
        HistoryOperation.CompleteChat, HistoryWorkload.Direct, HistoryOutcome.Succeeded,
        new(HistoryAuthenticationKind.Unknown), new(HistoryUsageState.Unavailable), new(HistoryPriceState.Unpriced),
        HistoryMetadataAuthority.Standalone, HistoryRetentionAuthority.HistoryPolicy, HistoryDetailState.Captured);

    internal HistoryEntry PrepareCanonical() {
        var entry = Entry() with { DetailState = HistoryDetailState.Canonical };
        var source = new CanonicalEvidenceReference(Partition, HistorySourceKind.SimpleChat, new("owner"), new("evidence"));
        Store.Metadata = new(entry, [new(entry.Id, source, new(1), HistoryOwnerRole.ContentOwner, HistoryOwnerState.Linked)]);
        Owner.Mutation = new(source, new(1), HistorySourceMutationKind.Upsert, entry, []);
        Owner.Detail = new(entry.Id, HistoryDetailState.Canonical, new("private prompt", 14, 14, HistoryDetailFlags.None));
        return entry;
    }

    public void Dispose() => Concurrency.Dispose();

    internal sealed class Access : IProviderHistoryAccess {
        internal HistoryAccessContext Context { get; set; } = new(Partition, new(1, 1), new(HistoryAuthenticationKind.TrustedLocalOperator), null) { AuthorizationStamp = "initial" };
        internal HashSet<HistoryPermission> Permissions { get; } = [HistoryPermission.ReadMetadata, HistoryPermission.ReadContent, HistoryPermission.Manage];
        internal bool OwnerDenied { get; set; }
        internal int OwnerChecks { get; private set; }
        public Task<HistoryAccessContext> AuthorizeAsync(HistoryPermission permission, CancellationToken cancellationToken) {
            Check(permission);
            return Task.FromResult(Context);
        }
        public Task EnsureCurrentAsync(HistoryAccessContext context, HistoryPermission permission, CancellationToken cancellationToken) {
            Check(permission);
            if (context != Context) {
                throw new ProviderHistoryException(HistoryFailure.StaleContext, "Changed");
            }
            return Task.CompletedTask;
        }
        public Task AuthorizeOwnerAsync(HistoryAccessContext context, CanonicalEvidenceReference owner, CancellationToken cancellationToken) {
            OwnerChecks++;
            if (OwnerDenied) {
                throw new ProviderHistoryException(HistoryFailure.Denied, "Owner denied");
            }
            return Task.CompletedTask;
        }
        private void Check(HistoryPermission permission) {
            if (!Permissions.Contains(permission)) {
                throw new ProviderHistoryException(HistoryFailure.Denied, "Denied");
            }
        }
    }

    internal sealed class Reader : IHistoryReadStore {
        internal int Searches { get; private set; }
        internal int ContentReads { get; private set; }
        internal HistoryIndexPage Page { get; set; } = new([], new(HistoryCoverageState.Current, Now), Now);
        internal HistoryMetadata? Metadata { get; set; }
        internal Func<CancellationToken, Task>? BeforeReturn { get; set; }
        internal bool Current { get; set; } = true;
        internal Action? AfterDetail { get; set; }
        public async Task<HistoryIndexPage> SearchAsync(HistoryAccessContext context, ProviderRequestHistoryQuery query, HistoryPagePosition? position, CancellationToken cancellationToken) {
            Searches++;
            if (BeforeReturn is not null) {
                await BeforeReturn(cancellationToken);
            }
            return Page;
        }
        public Task<HistoryMetadata?> GetMetadataAsync(HistoryAccessContext context, HistoryEntryId entryId, CancellationToken cancellationToken) => Task.FromResult(Metadata);
        public Task<HistoryDetail> ReadDetailAsync(HistoryAccessContext context, HistoryEntryId entryId, CancellationToken cancellationToken) {
            ContentReads++;
            AfterDetail?.Invoke();
            return Task.FromResult(new HistoryDetail(entryId, HistoryDetailState.Captured, new("private prompt", 14, 14, HistoryDetailFlags.None)));
        }
        public Task<bool> IsCurrentAsync(HistoryAccessContext context, HistoryMetadata metadata, CanonicalEvidenceReference? owner, CancellationToken cancellationToken) => Task.FromResult(Current);
    }

    internal sealed class Source : IProviderHistorySource {
        public HistorySourceKind Kind => HistorySourceKind.SimpleChat;
        internal HistorySourceMutation? Mutation { get; set; }
        internal HistoryDetail Detail { get; set; } = null!;
        internal Action? AfterDetail { get; set; }
        internal int ContentReads { get; private set; }
        public Task<HistorySourceMutation?> ReadAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken) => Task.FromResult(Mutation);
        public Task<HistoryDetail> ReadDetailAsync(CanonicalEvidenceReference source, HistoryEntryId entryId, CancellationToken cancellationToken) {
            ContentReads++;
            AfterDetail?.Invoke();
            return Task.FromResult(Detail);
        }
    }
}
