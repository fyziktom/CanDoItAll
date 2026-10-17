using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class CanonicalAgentExecutionAuthorityResolverTests {
    [Fact]
    public async Task Historical_read_preserves_original_admission_and_requires_current_source_catalog_and_host_fences() {
        var allowed = new HistoricalAuthorityFixture();
        var original = allowed.Original;

        var policy = await allowed.ObserveAsync();

        Assert.Equal(AgentHistoricalReceiptReadPolicy.Allowed, policy);
        Assert.Same(original, allowed.Original);
        Assert.Equal(new DatabaseProfileGeneration(8), original.Session.Profile.Generation);
        Assert.Equal(new DatabaseProfileGeneration(0), allowed.Fence.Profile.Generation);
        Assert.Same(original.Governance, allowed.Source.LastRequest!.Revalidation!.Authority);
        Assert.Same(original.TurnContext, allowed.Source.LastRequest.Revalidation.Source);
        Assert.Equal(ProjectLifetime, allowed.Source.LastRequest.ObservedProjectLifetime);
        Assert.Equal(original.Governance!.WorkspaceScope, allowed.Source.LastRequest.ObservedWorkspaceScope);
        Assert.False(allowed.Source.Decision.MutationAllowed);
        Assert.Equal(2, allowed.Catalog.Reads);
        Assert.Equal(1, allowed.Source.Reads);
        Assert.Equal(0, allowed.Workspaces.Reads);
        Assert.Equal(0, allowed.Catalog.Leases);

        await Assert.ThrowsAsync<InvalidOperationException>(() => allowed.Resolver.ResolveAsync(
            AgentExecutionAuthorityResolutionRequest.FromCaptured(original.TurnContext!, original.Governance)).AsTask());
        Assert.Equal(2, allowed.Catalog.Reads);
        Assert.Equal(1, allowed.Source.Reads);
        Assert.Equal(0, allowed.Workspaces.Reads);

        await RequirePolicyAsync("current source read revoked", fixture => {
            fixture.Source.Decision = fixture.Source.Decision with { ReadAllowed = false };
        }, AgentHistoricalReceiptReadPolicy.Denied);
        await RequirePolicyAsync("current source scope replaced", fixture => {
            fixture.Source.Decision = fixture.Source.Decision with { WorkspaceScope = WorkspaceScopeDescriptor.Sandbox };
        }, AgentHistoricalReceiptReadPolicy.Denied);
        await RequirePolicyAsync("current project lifetime replaced", fixture => {
            fixture.Source.Decision = fixture.Source.Decision with {
                SourceProjectLifetime = new(ProfileId, ProjectId, Guid.NewGuid())
            };
        }, AgentHistoricalReceiptReadPolicy.Denied);
        await RequirePolicyAsync("current source owner denies", fixture => {
            fixture.Source.BeforeReturn = _ => throw new AgentExecutionAuthorityMismatchException("Source access changed.");
        }, AgentHistoricalReceiptReadPolicy.Denied);
        await RequirePolicyAsync("catalog agent removed before source", fixture => {
            fixture.Catalog.Observe = _ => fixture.Catalog.Ready with { Status = AgentHistoricalStorageReadStatus.Missing, Agent = null };
        }, AgentHistoricalReceiptReadPolicy.Denied, expectedSourceReads: 0);
        await RequirePolicyAsync("catalog agent inactive before source", fixture => {
            fixture.Catalog.Observe = _ => fixture.Catalog.Ready with { Agent = fixture.Agent with { Status = AgentLifecycleStatus.Draft } };
        }, AgentHistoricalReceiptReadPolicy.Denied, expectedSourceReads: 0);
        await RequirePolicyAsync("catalog agent removed after source", fixture => {
            fixture.Catalog.Observe = read => read == 1 ? fixture.Catalog.Ready :
                fixture.Catalog.Ready with { Status = AgentHistoricalStorageReadStatus.Missing, Agent = null };
        }, AgentHistoricalReceiptReadPolicy.Denied);
        await RequirePolicyAsync("generation changed during final catalog observation", fixture => {
            fixture.Catalog.Observe = read => {
                if (read == 2) {
                    fixture.Generation.Current = new(1);
                }
                return fixture.Catalog.Ready;
            };
        }, AgentHistoricalReceiptReadPolicy.ContextChanged);

        var changedCatalog = new HistoricalAuthorityFixture();
        changedCatalog.Catalog.Observe = read => changedCatalog.Catalog.Ready with { Revision = new(read) };
        var catalogFailure = await Record.ExceptionAsync(() => changedCatalog.ObserveAsync());
        Assert.True(catalogFailure is IOException, "A changed catalog revision after source observation must be read-unavailable.");
        Assert.Equal(2, changedCatalog.Catalog.Reads);
        Assert.Equal(0, changedCatalog.Workspaces.Reads);

        var waiting = new HistoricalAuthorityFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting.Source.BeforeReturn = async cancellationToken => {
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
        };
        var pending = waiting.ObserveAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted, "The source observation must actually remain pending before changing the host generation.");
            waiting.Generation.Current = new(1);
        } finally {
            release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(AgentHistoricalReceiptReadPolicy.ContextChanged, await pending);
        Assert.Equal(1, waiting.Catalog.Reads);
        Assert.Equal(0, waiting.Workspaces.Reads);
        Assert.Same(waiting.Original.Governance, waiting.Source.LastRequest!.Revalidation!.Authority);

        static async Task RequirePolicyAsync(string arrangement, Action<HistoricalAuthorityFixture> arrange,
            AgentHistoricalReceiptReadPolicy expected, int expectedSourceReads = 1) {
            var fixture = new HistoricalAuthorityFixture();
            arrange(fixture);
            var actual = await fixture.ObserveAsync();
            Assert.True(actual == expected, $"{arrangement}: expected {expected}, received {actual}.");
            Assert.True(fixture.Source.Reads == expectedSourceReads, $"{arrangement}: unexpected source observation count.");
            Assert.True(fixture.Workspaces.Reads == 0 && fixture.Catalog.Leases == 0,
                $"{arrangement}: historical observation attempted an ordinary workspace read or a lease.");
        }
    }

    [Fact]
    public async Task Historical_read_requires_explicit_capability_and_never_falls_back_to_ordinary_authority() {
        var unsupportedCatalog = new HistoricalAuthorityFixture();
        var resolverWithoutCatalog = new CanonicalAgentExecutionAuthorityResolver(unsupportedCatalog.Workspaces,
            unsupportedCatalog.Profile, unsupportedCatalog.Generation, TimeProvider.System,
            [unsupportedCatalog.Source], new LeaseOnlyHistoricalCatalog());
        Assert.Equal(AgentHistoricalReceiptReadPolicy.Unsupported,
            await resolverWithoutCatalog.ObserveHistoricalReceiptReadAsync(unsupportedCatalog.Original, unsupportedCatalog.Fence));
        Assert.Equal(0, unsupportedCatalog.Catalog.Reads);
        Assert.Equal(0, unsupportedCatalog.Source.Reads);
        Assert.Equal(0, unsupportedCatalog.Workspaces.Reads);

        var defaultObserver = new OrdinaryOnlyHistoricalResolver();
        Assert.Equal(AgentHistoricalReceiptReadPolicy.Unsupported,
            await ((IAgentExecutionAuthorityResolver)defaultObserver).ObserveHistoricalReceiptReadAsync(
                unsupportedCatalog.Original, unsupportedCatalog.Fence));
        Assert.Equal(0, defaultObserver.Resolutions);

        var unsupportedSource = new HistoricalAuthorityFixture();
        var resolverWithoutSource = new CanonicalAgentExecutionAuthorityResolver(unsupportedSource.Workspaces,
            unsupportedSource.Profile, unsupportedSource.Generation, TimeProvider.System,
            [], unsupportedSource.Catalog);
        var sourcePolicy = await resolverWithoutSource.ObserveHistoricalReceiptReadAsync(unsupportedSource.Original, unsupportedSource.Fence);
        Assert.True(sourcePolicy == AgentHistoricalReceiptReadPolicy.Unsupported,
            "A missing source-owned observer must not fall back to ordinary authority resolution.");
        Assert.Equal(1, unsupportedSource.Catalog.Reads);
        Assert.Equal(0, unsupportedSource.Source.Reads);
        Assert.Equal(0, unsupportedSource.Workspaces.Reads);

        var legacy = new HistoricalAuthorityFixture();
        var authority = legacy.Original.Governance!;
        var legacyAuthority = new AgentExecutionGovernanceSnapshot(authority.AuthorityId, authority.AgentId,
            authority.DatabaseProfileId, authority.DatabaseProfileGeneration, authority.WorkspaceScope,
            authority.ReadAllowed, authority.MutationAllowed, authority.PolicyVersion, authority.PolicyFingerprint);
        var legacyPolicy = await legacy.Resolver.ObserveHistoricalReceiptReadAsync(
            legacy.Original with { Governance = legacyAuthority }, legacy.Fence);
        Assert.True(legacyPolicy == AgentHistoricalReceiptReadPolicy.Unsupported,
            "A legacy admission without its original project lifetime must not be upgraded for historical review.");
        Assert.Equal(0, legacy.Source.Reads);

        var mismatchedProfile = new HistoricalAuthorityFixture();
        var changedSession = mismatchedProfile.Original.Session with {
            Profile = new(ProfileId, "different-runtime-fingerprint", new(8))
        };
        var mismatch = await mismatchedProfile.Resolver.ObserveHistoricalReceiptReadAsync(
            mismatchedProfile.Original with { Session = changedSession }, mismatchedProfile.Fence);
        Assert.True(mismatch == AgentHistoricalReceiptReadPolicy.Denied,
            "A saved runtime binding mismatch must be denied before catalog or owner reads.");
        Assert.Equal(0, mismatchedProfile.Catalog.Reads);
        Assert.Equal(0, mismatchedProfile.Source.Reads);

        var changedProfileId = changedSession with { Profile = new(Guid.NewGuid(), mismatchedProfile.Original.Session.Profile.Fingerprint, new(8)) };
        Assert.Equal(AgentHistoricalReceiptReadPolicy.Denied, await mismatchedProfile.Resolver.ObserveHistoricalReceiptReadAsync(
            mismatchedProfile.Original with { Session = changedProfileId }, mismatchedProfile.Fence));
        var unselectedBasis = new AgentHistoricalCatalogBasis(mismatchedProfile.Catalog, mismatchedProfile.Fence,
            () => mismatchedProfile.Generation.Current == mismatchedProfile.Fence.Profile.Generation);
        var observer = new CanonicalAgentHistoricalReceiptReadObserver(unselectedBasis, []);
        try {
            foreach (var session in new[] { changedSession, changedProfileId }) {
                Assert.Equal(AgentHistoricalReceiptReadPolicy.Denied, await observer.ObserveAsync(
                    mismatchedProfile.Original with { Session = session }, mismatchedProfile.Fence, default));
            }
            Assert.Equal(0, mismatchedProfile.Catalog.Reads);
            Assert.Equal(0, mismatchedProfile.Source.Reads);
            mismatchedProfile.Generation.Current = new(1);
            Assert.Equal(AgentHistoricalReceiptReadPolicy.ContextChanged, await mismatchedProfile.Resolver.ObserveHistoricalReceiptReadAsync(
                mismatchedProfile.Original with { Session = changedSession }, mismatchedProfile.Fence));
            await Assert.ThrowsAsync<AgentHistoricalReceiptContextChangedException>(() => observer.ObserveAsync(
                mismatchedProfile.Original with { Session = changedSession }, mismatchedProfile.Fence, default).AsTask());
            Assert.Equal(0, mismatchedProfile.Catalog.Reads);
        } finally {
            unselectedBasis.Retire();
        }

        AgentHistoricalStorageReadStatus[] unavailableStatuses = [AgentHistoricalStorageReadStatus.RecoveryRequired,
            AgentHistoricalStorageReadStatus.UnsupportedLayout, AgentHistoricalStorageReadStatus.SizeLimitExceeded,
            AgentHistoricalStorageReadStatus.SnapshotChanged];
        foreach (var status in unavailableStatuses) {
            var unavailable = new HistoricalAuthorityFixture();
            unavailable.Catalog.Observe = _ => unavailable.Catalog.Ready with { Status = status, Agent = null };
            var failure = await Record.ExceptionAsync(() => unavailable.ObserveAsync());
            Assert.True(failure is IOException, $"Catalog {status} must be unavailable, never repaired by an ordinary read.");
            Assert.True(unavailable.Workspaces.Reads == 0 && unavailable.Catalog.Leases == 0 && unavailable.Source.Reads == 0,
                $"Catalog {status} must refuse before ordinary workspace access, lease acquisition or source observation.");
        }
    }

    private sealed class HistoricalAuthorityFixture {
        private const string RuntimeFingerprint = "historical-authority-fixture";

        public HistoricalAuthorityFixture() {
            Agent = CreateAgent(new AgentProjectStructureAccessSettings { CanRead = true, AllowAllProjects = true });
            var profile = new FixedDatabaseProfileRuntimeAccessor(ProfileId);
            profile.Profile.Profile.Runtime.Fingerprint = RuntimeFingerprint;
            Profile = profile;
            var fingerprint = profile.ResolveCurrentProfile().Profile.Runtime.Fingerprint;
            var governance = new AgentExecutionGovernanceSnapshot(AgentExecutionAuthorityId.Create(), Agent.Id, ProfileId,
                new(8), WorkspaceScopeDescriptor.Project(ProjectId.ToString("D")), true, true,
                AgentExecutionAuthorityPolicyVersions.Canonical, "saved-policy-fingerprint",
                schemaVersion: AgentExecutionAuthorityRecord.CurrentSchemaVersion, sourceProjectLifetime: ProjectLifetime);
            Original = new(new(new(Guid.NewGuid(), Guid.NewGuid(), governance.AuthorityId), Agent.Id,
                AgentRuntimeContextPurpose.InteractiveChat, new(ProfileId, fingerprint, new(8))),
                CreateRevalidationReference(CreateRequest(Agent.Id)), governance);
            Catalog = new(Agent);
            Fence = new(new(ProfileId, fingerprint, new(0)), Catalog.Ready.Scope);
            Resolver = new(Workspaces, profile, Generation, TimeProvider.System, [Source], Catalog);
        }

        public AgentDefinition Agent { get; }
        public FixedDatabaseProfileRuntimeAccessor Profile { get; }
        public AgentToolSessionObservation Original { get; }
        public AgentHistoricalReceiptReadFence Fence { get; }
        public HistoricalCatalog Catalog { get; }
        public HistoricalSourceProvider Source { get; } = new();
        public HistoricalProfileGeneration Generation { get; } = new();
        public RejectingHistoricalWorkspaceFactory Workspaces { get; } = new();
        public CanonicalAgentExecutionAuthorityResolver Resolver { get; }
        public Task<AgentHistoricalReceiptReadPolicy> ObserveAsync() => Resolver.ObserveHistoricalReceiptReadAsync(Original, Fence).AsTask();
    }

    private sealed class HistoricalProfileGeneration : IAgentExecutionProfileGenerationSource {
        public DatabaseProfileGeneration Current { get; set; } = new(0);
        public DatabaseProfileGeneration GetGeneration() => Current;
    }

    private sealed class HistoricalCatalog(AgentDefinition agent) : IAgentCatalogReadLeaseStore, IAgentHistoricalCatalogReadStore {
        public AgentHistoricalAgentRead Ready { get; } = new(AgentHistoricalStorageReadStatus.Ready,
            WorkspaceScopeDescriptor.Organization(ProfileId.ToString("N")), new(1), agent);
        public Func<int, AgentHistoricalAgentRead>? Observe { get; set; }
        public int Reads { get; private set; }
        public int Leases { get; private set; }

        public Task<AgentHistoricalAgentRead> ReadHistoricalAgentAsync(Guid agentId, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(agent.Id, agentId);
            Reads++;
            return Task.FromResult(Observe?.Invoke(Reads) ?? Ready);
        }

        public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
            Leases++;
            throw new InvalidOperationException("Historical receipt observation must not acquire a catalog lease.");
        }
    }

    private sealed class HistoricalSourceProvider : IAgentExecutionSourceAuthorityProvider {
        public string SourceKind => AgentChatTrustedSourceKinds.ProjectStructure;
        public int Reads { get; private set; }
        public AgentExecutionSourceAuthorityRequest? LastRequest { get; private set; }
        public Func<CancellationToken, Task>? BeforeReturn { get; set; }
        public AgentExecutionSourceAuthorityDecision Decision { get; set; } = new(
            WorkspaceScopeDescriptor.Project(ProjectId.ToString("D")), true, false, AgentExecutionAuthorityPolicyVersions.Canonical) {
            SourceProjectLifetime = ProjectLifetime
        };

        public async ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(AgentExecutionSourceAuthorityRequest request,
            CancellationToken cancellationToken = default) {
            Reads++;
            LastRequest = request;
            if (BeforeReturn is not null) {
                await BeforeReturn(cancellationToken);
            }
            return Decision;
        }
    }

    private sealed class RejectingHistoricalWorkspaceFactory : ICanDoItAllAgentWorkspaceFactory {
        public int Reads { get; private set; }
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() {
            Reads++;
            throw new InvalidOperationException("Historical receipt observation must use the explicit read-only catalog port.");
        }
        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) => GetOrganizationWorkspaceService();
        public WorkspaceScopeDescriptor GetOrganizationScope() => WorkspaceScopeDescriptor.Organization(ProfileId.ToString("N"));
        public string GetWorkspaceRoot() => throw new InvalidOperationException("Historical receipt observation must not inspect workspace files directly.");
    }

    private sealed class LeaseOnlyHistoricalCatalog : IAgentCatalogReadLeaseStore {
        public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("An unsupported historical catalog must not fall back to a lease.");
    }

    private sealed class OrdinaryOnlyHistoricalResolver : IAgentExecutionAuthorityResolver {
        public int Resolutions { get; private set; }
        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default) {
            Resolutions++;
            throw new InvalidOperationException("An unsupported historical observer must not fall back to execution admission.");
        }
    }
}
