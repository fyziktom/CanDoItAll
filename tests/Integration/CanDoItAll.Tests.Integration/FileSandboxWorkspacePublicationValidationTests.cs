using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class FileSandboxWorkspacePublicationValidationTests {
    [Fact]
    public Task Final_validation_observes_one_frozen_target_after_key_wait_without_replaying_callback() => RunAsync(async fixture => {
        var tags = new List<string> { "prepared" };
        var agents = fixture.Before.Agents.Select(agent => agent.Id == fixture.Agent.Id
            ? agent with { Name = "Prepared target", Tags = tags } : agent).ToList();
        var target = fixture.Before with { Agents = agents };
        await fixture.HoldAsync(target);
        using var wait = new PublicationWait();
        var callbacks = 0;
        var validations = 0;
        var pending = fixture.Track(fixture.Store.UpdateCatalogAsync(current => {
            callbacks++;
            Assert.Equal(fixture.Before.CatalogDataRevision, current.CatalogDataRevision);
            return target;
        }, prepared => {
            validations++;
            var selected = prepared.Agents.Single(agent => agent.Id == fixture.Agent.Id);
            Assert.Equal("Prepared target", selected.Name);
            Assert.Equal(new[] { "prepared" }, selected.Tags);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)selected.Tags).Add("validator mutation"));
            Assert.Throws<NotSupportedException>(() => ((IList<AgentDefinition>)prepared.Agents).Clear());
        }, fixture.Token));
        await wait.Entered.Task.WaitAsync(fixture.Token);
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, callbacks);
        Assert.Equal(0, validations);
        await fixture.RequireWorkspaceAvailableAsync();
        Assert.Equal(fixture.Before.CatalogDataRevision,
            (await fixture.NewStore().LoadCatalogAsync(fixture.Token)).CatalogDataRevision);
        tags.Add("changed during key wait");
        agents.Clear();
        await fixture.ReleaseAsync();
        var saved = await pending.WaitAsync(fixture.Token);
        Assert.Equal(1, callbacks);
        Assert.Equal(1, validations);
        Assert.Equal(fixture.Before.CatalogDataRevision.Next(), saved.CatalogDataRevision);
        var reloaded = await fixture.NewStore().LoadCatalogAsync(fixture.Token);
        Assert.Equal(saved.CatalogDataRevision, reloaded.CatalogDataRevision);
        Assert.Equal("Prepared target", reloaded.Agents.Single(agent => agent.Id == fixture.Agent.Id).Name);
        Assert.Equal(new[] { "prepared" }, reloaded.Agents.Single(agent => agent.Id == fixture.Agent.Id).Tags);
    });

    [Fact]
    public Task Noop_still_runs_final_validation_and_refusal_preserves_exact_catalog_and_index() => RunAsync(async fixture => {
        var beforeCatalog = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        var beforeIndex = await File.ReadAllBytesAsync(fixture.Layout.WorkspaceIndexPath, fixture.Token);
        var callbacks = 0;
        var validations = 0;
        var saved = await fixture.Store.UpdateCatalogAsync(current => {
            callbacks++;
            return current;
        }, prepared => {
            validations++;
            Assert.Equal(fixture.Before.CatalogDataRevision, prepared.CatalogDataRevision);
        }, fixture.Token);
        var rejection = new InvalidOperationException("The final publication condition changed.");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.UpdateCatalogAsync(current => {
            callbacks++;
            return current;
        }, _ => {
            validations++;
            throw rejection;
        }, fixture.Token));
        Assert.Same(rejection, actual);
        Assert.Equal(2, callbacks);
        Assert.Equal(2, validations);
        Assert.Equal(fixture.Before.CatalogDataRevision, saved.CatalogDataRevision);
        Assert.Equal(beforeCatalog, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
        Assert.Equal(beforeIndex, await File.ReadAllBytesAsync(fixture.Layout.WorkspaceIndexPath, fixture.Token));
    });

    [Fact]
    public Task Unsupported_owner_refuses_final_validation_without_invoking_legacy_write_or_replaying_diagnostic() => RunAsync(async fixture => {
        var legacy = new LegacyCatalogOwner(fixture.Store);
        ISandboxWorkspaceCatalogStore owner = legacy;
        var callbacks = 0;
        await Assert.ThrowsAsync<NotSupportedException>(async () => await owner.UpdateCatalogAsync(current => {
            callbacks++;
            return current;
        }, _ => callbacks++, fixture.Token));
        var before = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        var proof = new CountingProof();
        var providers = new ExternalProviderSnapshots(fixture.Provider);
        var outcome = await new CapabilityVerificationPublication(owner, proof, providers)
            .ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, fixture.Token);
        Assert.Equal(CapabilityVerificationDisposition.PublicationNotStarted, outcome.Disposition);
        var receipt = Assert.IsType<CapabilityProofReceipt>(outcome.Receipt);
        var current = await fixture.NewStore().LoadCatalogAsync(fixture.Token);
        Assert.Equal(CapabilityProofRecovery.NotPublished, receipt.Classify(current.Agents, current.Capabilities));
        Assert.Equal(0, callbacks);
        Assert.Equal(0, legacy.WriteCalls);
        Assert.Equal(1, proof.Calls);
        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
    });

    [Fact]
    public Task Provider_invalidation_during_real_key_wait_refuses_capability_publication_without_catalog_change() => RunAsync(async fixture => {
        await fixture.HoldAsync(fixture.Before with {
            Agents = fixture.Before.Agents.Select(agent => agent.Id == fixture.Agent.Id
                ? agent with { Name = "Key selection only" } : agent).ToArray()
        });
        var beforeCatalog = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        var beforeIndex = await File.ReadAllBytesAsync(fixture.Layout.WorkspaceIndexPath, fixture.Token);
        var proof = new CountingProof();
        var providers = new ExternalProviderSnapshots(fixture.Provider);
        using var wait = new PublicationWait();
        var pending = fixture.Track(new CapabilityVerificationPublication(fixture.Store, proof, providers)
            .ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, fixture.Token));
        await wait.Entered.Task.WaitAsync(fixture.Token);
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, proof.Calls);
        Assert.Equal(3, providers.Captures);
        await fixture.RequireWorkspaceAvailableAsync();
        Assert.Equal(fixture.Before.CatalogDataRevision,
            (await fixture.NewStore().LoadCatalogAsync(fixture.Token)).CatalogDataRevision);
        providers.Invalidate();
        await fixture.ReleaseAsync();
        var outcome = await pending.WaitAsync(fixture.Token);
        Assert.Equal(CapabilityVerificationDisposition.Superseded, outcome.Disposition);
        Assert.Equal(4, providers.Captures);
        Assert.Equal(1, proof.Calls);
        var current = await fixture.NewStore().LoadCatalogAsync(fixture.Token);
        Assert.Equal(CapabilityProofRecovery.NotPublished, outcome.Receipt!.Classify(current.Agents, current.Capabilities));
        Assert.Equal(CapabilityProofStatus.NotRun,
            current.Agents.Single(agent => agent.Id == fixture.Agent.Id).Capabilities.Single().ProofStatus);
        Assert.Equal(beforeCatalog, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
        Assert.Equal(beforeIndex, await File.ReadAllBytesAsync(fixture.Layout.WorkspaceIndexPath, fixture.Token));
    });

    private static async Task RunAsync(Func<PublicationFixture, Task> test) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var environment = CanDoItAllTestEnvironment.Create("publication-validation");
        PublicationFixture? fixture = null;
        var failures = new List<Exception>();
        try {
            fixture = new PublicationFixture(environment.RootPath, deadline.Token);
            await fixture.SeedAsync();
            await test(fixture);
        } catch (Exception failure) {
            failures.Add(failure);
        } finally {
            try {
                await deadline.CancelAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            try {
                if (fixture is not null) {
                    await fixture.ReleaseAsync();
                }
            } catch (Exception failure) {
                failures.Add(failure);
            }
            foreach (var pending in fixture?.Pending ?? []) {
                try {
                    await pending;
                } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                } catch (Exception failure) {
                    if (!failures.Contains(failure)) {
                        failures.Add(failure);
                    }
                }
            }
            try {
                await environment.DisposeAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1) {
            throw new AggregateException("Publication validation and owned cleanup failed.", failures);
        }
    }

    private sealed class PublicationFixture(string root, CancellationToken token) {
        private IAsyncDisposable? held;
        private readonly Guid agentId = Guid.NewGuid();
        public CancellationToken Token { get; } = token;
        public FileSandboxWorkspaceStorageLayout Layout { get; } = new(root);
        public FileSandboxWorkspaceStore Store { get; } = new(root);
        public SandboxWorkspaceCatalog Before { get; private set; } = default!;
        public AgentDefinition Agent => Before.Agents.Single(agent => agent.Id == agentId);
        public ProviderProfile Provider => Before.Providers.Single(item => item.Id == Agent.ProviderProfileId);
        public CapabilityCatalogItem Capability { get; } = new(Guid.NewGuid(), CapabilityKind.Skill,
            "publication-inline-proof", "Publication inline proof", "Local proof fixture", "inline://publication-proof",
            """{"inlineSkill":{"instructions":"Read the local fixture."}}""", CapabilityProofStatus.NotRun, "", null, false);
        public List<Task> Pending { get; } = [];

        public FileSandboxWorkspaceStore NewStore() => new(root);

        public async Task SeedAsync() {
            var catalog = await Store.LoadCatalogAsync(Token);
            var agent = catalog.Agents.First(item => !item.IsTemplate) with {
                Id = agentId, Name = "Publication validation", TemplateKey = "", ConfigurationJson = "{}",
                ProviderProfileId = catalog.Providers.First().Id,
                Capabilities = [new(Capability.Id, Capability.Key, Capability.Kind, CapabilityProofStatus.NotRun, null, "")]
            };
            Before = await Store.UpdateCatalogAsync(current => current with {
                Agents = [.. current.Agents, agent], Capabilities = [.. current.Capabilities, Capability]
            }, Token);
        }

        public async Task HoldAsync(SandboxWorkspaceCatalog target) {
            var path = Assert.Single(FileSandboxWorkspaceWriteSet.Catalog(Before, target).LockPaths(Layout));
            held = await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory())
                .AcquireCoordinationAsync(root, path, TimeSpan.FromSeconds(5), false, Token);
        }

        public async Task ReleaseAsync() {
            var lease = held;
            held = null;
            if (lease is not null) {
                await lease.DisposeAsync();
            }
        }

        public async Task RequireWorkspaceAvailableAsync() {
            await using var workspace = await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory())
                .AcquireCoordinationAsync(root, Layout.WorkspaceLockPath, TimeSpan.FromSeconds(5), false, Token);
        }

        public Task<T> Track<T>(Task<T> pending) {
            Pending.Add(pending);
            return pending;
        }
    }

    private sealed class PublicationWait : IDisposable {
        private readonly Activity root = new Activity("publication-validation").SetIdFormat(ActivityIdFormat.W3C).Start();
        private readonly ActivityListener listener;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PublicationWait() {
            listener = new() {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.publication.keys.wait") {
                        Entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public void Dispose() {
            listener.Dispose();
            root.Dispose();
        }
    }

    private sealed class CountingProof : ICapabilityProofService {
        private readonly CapabilityProofService actual = new(new PhysicalFileSystemPathPolicyFactory());
        public int Calls { get; private set; }

        public Task<CapabilityVerificationResult> VerifyAsync(AgentDefinition agent, ProviderProfile? provider,
            CapabilityCatalogItem capability, CancellationToken cancellationToken = default) {
            Calls++;
            return actual.VerifyAsync(agent, provider, capability, cancellationToken);
        }
    }

    private sealed class ExternalProviderSnapshots(ProviderProfile profile) : IProviderRuntimeProfileSnapshotSource {
        private ProviderRuntimeProfileSnapshotLease current = new(profile,
            ProviderConfigurationFingerprintFactory.Create(profile), new(Guid.NewGuid()));
        private int captures;
        public int Captures => Volatile.Read(ref captures);

        public void Invalidate() {
            var previous = Volatile.Read(ref current);
            Volatile.Write(ref current, previous with { ConfigurationRevision = new(Guid.NewGuid()) });
        }

        public ProviderRuntimeProfileSnapshotLease? CaptureProvider(Guid providerId, SandboxWorkspaceCatalogSnapshot snapshot) {
            Assert.Equal(snapshot.Catalog.CatalogDataRevision, snapshot.Revision);
            var observed = Volatile.Read(ref current);
            Assert.Equal(observed.Profile.Id, providerId);
            Interlocked.Increment(ref captures);
            return observed;
        }
    }

    private sealed class LegacyCatalogOwner(FileSandboxWorkspaceStore actual) : ISandboxWorkspaceCatalogStore {
        public int WriteCalls { get; private set; }

        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => actual.LoadCatalogAsync(cancellationToken);

        public Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(CancellationToken cancellationToken = default)
            => actual.LoadCatalogSnapshotAsync(cancellationToken);

        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(SandboxWorkspaceCatalog catalog, CancellationToken cancellationToken = default)
            => RefuseLegacyWrite();

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default) => RefuseLegacyWrite();

        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision, CancellationToken cancellationToken = default) => RefuseLegacyWrite();

        private Task<SandboxWorkspaceCatalog> RefuseLegacyWrite() {
            WriteCalls++;
            throw new InvalidOperationException("Final validation must not fall back to a legacy catalog writer.");
        }
    }
}
