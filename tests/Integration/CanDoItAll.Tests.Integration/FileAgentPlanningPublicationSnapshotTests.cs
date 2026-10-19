using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class FileAgentPlanningPublicationSnapshotTests {
    [Fact]
    public Task Held_selected_keys_allow_unrelated_publication_and_exclude_source_changes() => RunAsync(async scenario => {
        var before = await scenario.Store.LoadCatalogAsync(scenario.Token);
        var unrelated = before.Agents.Single(agent => agent.Id == scenario.UnrelatedId);
        var lease = await scenario.AcquireAsync();
        Assert.Equal(scenario.Expected, lease.Original);
        AssertOriginalRun(scenario.Run, lease.OriginalRun);
        Assert.Equal(scenario.Run.ChatSessionId, lease.OriginalChatSession.Id);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)lease.Agent.Tags).Add("caller alias"));
        Assert.Throws<NotSupportedException>(() => ((IList<AgentDefinition>)lease.Agents).Clear());
        scenario.RequireAllSelectedKeysHeld();
        await scenario.RequireWorkspaceAvailableAsync();
        await scenario.Store.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => agent.Id == unrelated.Id
                ? agent with { Summary = "Independent publication completed" } : agent).ToArray()
        }, scenario.Token);
        var independent = await scenario.Store.LoadCatalogAsync(scenario.Token);
        Assert.True(independent.CatalogDataRevision.Value > lease.Revision.Value);
        Assert.Equal("Independent publication completed", independent.Agents.Single(agent => agent.Id == unrelated.Id).Summary);
        lease.RequireActive();
        Assert.Equal(before.CatalogDataRevision, lease.Revision);
        Assert.Equal(scenario.Run.MetadataJson, lease.OriginalRun.MetadataJson);

        using var sourceWait = new KeyWait();
        var source = scenario.Track(scenario.Store.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => agent.Id == scenario.Agent.Id
                ? agent with { Permissions = agent.Permissions with { CanUseTools = false } } : agent).ToArray()
        }, scenario.Token));
        await sourceWait.Entered.Task.WaitAsync(scenario.Token);
        Assert.False(source.IsCompleted);
        await scenario.RequireWorkspaceAvailableAsync();
        await scenario.ReleaseLeaseAsync();
        await scenario.ObserveAsync(source);
        Assert.Throws<ObjectDisposedException>(lease.RequireActive);
        Assert.Throws<ObjectDisposedException>(() => lease.OriginalRun);
        Assert.False((await scenario.Store.LoadCatalogAsync(scenario.Token)).Agents.Single(agent => agent.Id == scenario.Agent.Id).Permissions.CanUseTools);
        await scenario.RequireAllKeysAvailableAsync();
    });

    [Fact]
    public Task Original_run_writer_waits_until_the_authorization_snapshot_is_released() => RunAsync(async scenario => {
        var lease = await scenario.AcquireAsync();
        using var wait = new KeyWait();
        var run = scenario.Track(((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store).UpdateExecutionRunDetailAsync(
            scenario.Run.Id, current => current with { Run = current.Run with {
                Revision = checked(current.Run.Revision + 1), ResultSummary = "Selected checkpoint completed"
            } }, scenario.Token));
        await wait.Entered.Task.WaitAsync(scenario.Token);
        Assert.False(run.IsCompleted);
        AssertOriginalRun(scenario.Run, lease.OriginalRun);
        await scenario.RequireWorkspaceAvailableAsync();
        await scenario.ReleaseLeaseAsync();
        await scenario.ObserveAsync(run);
        var saved = (await scenario.Store.GetExecutionRunAsync(scenario.Run.Id, scenario.Token))!;
        Assert.Equal(scenario.Run.Revision + 1, saved.Revision);
        Assert.Equal("Selected checkpoint completed", saved.ResultSummary);
        await scenario.RequireAllKeysAvailableAsync();
    });

    [Fact]
    public Task Current_revocation_and_complete_capability_configuration_do_not_replace_original_decision_inputs() => RunAsync(async scenario => {
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Tool, "publication-authority-test",
            "Publication authority fixture", "", "fixture-endpoint", "{\"effectAuthority\":\"read-only\"}",
            CapabilityProofStatus.NotRun, "", null, false) { Tags = ["authority-fixture"] };
        var currentConfiguration = "{\"projectStructure\":{\"canRead\":false},\"opaqueEffectAuthority\":{\"write\":false}}";
        var published = await scenario.Store.UpdateCatalogAsync(current => current with {
            Capabilities = [.. current.Capabilities, capability],
            Agents = current.Agents.Select(agent => agent.Id == scenario.Agent.Id ? agent with {
                Status = AgentLifecycleStatus.Suspended,
                Permissions = agent.Permissions with { CanUseTools = false },
                ConfigurationJson = currentConfiguration,
                Capabilities = [new(capability.Id, capability.Key, capability.Kind, capability.ProofStatus, null, "")]
            } : agent).ToArray()
        }, scenario.Token);
        var acknowledged = published.Agents.Single(agent => agent.Id == scenario.Agent.Id);
        var lease = await scenario.AcquireAsync();
        Assert.Equal(AgentLifecycleStatus.Suspended, lease.Agent.Status);
        Assert.False(lease.Agent.Permissions.CanUseTools);
        Assert.Equal(acknowledged.ConfigurationJson, lease.Agent.ConfigurationJson);
        var heldCapability = Assert.Single(lease.Capabilities);
        Assert.Equal(capability.Id, heldCapability.Id);
        Assert.Equal(capability.ConfigurationJson, heldCapability.ConfigurationJson);
        Assert.Equal(capability.EndpointOrPath, heldCapability.EndpointOrPath);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)heldCapability.Tags).Clear());
        Assert.Equal(scenario.Run.MetadataJson, lease.OriginalRun.MetadataJson);
        Assert.Equal(scenario.Run.InputSummary, lease.OriginalRun.InputSummary);
        AssertOriginalRun(scenario.Run, lease.OriginalRun);
        Assert.Equal(scenario.Expected.Session, lease.Original.Session);
    });

    [Fact]
    public Task Original_scope_authority_profile_and_revisions_are_not_replaced_by_current_values() => RunAsync(async scenario => {
        var expected = scenario.Expected;
        var session = expected.Session;
        var references = new[] {
            new AgentPlanningPublicationExpectation(expected.Scope, session, expected.RunRevision + 1, expected.JournalRevision),
            new AgentPlanningPublicationExpectation(expected.Scope, session, expected.RunRevision, expected.JournalRevision + 1),
            new AgentPlanningPublicationExpectation(expected.Scope, session with { AgentId = Guid.NewGuid() }, expected.RunRevision, expected.JournalRevision),
            new AgentPlanningPublicationExpectation(expected.Scope, session with { Reference = new(session.Reference.ExecutionRunId,
                session.Reference.ChatSessionId, AgentExecutionAuthorityId.Create()) }, expected.RunRevision, expected.JournalRevision),
            new AgentPlanningPublicationExpectation(expected.Scope, session with { Profile = new(session.Profile.ProfileId,
                "different-original-fingerprint", session.Profile.Generation) }, expected.RunRevision, expected.JournalRevision),
            new AgentPlanningPublicationExpectation(expected.Scope, session with { Profile = new(session.Profile.ProfileId,
                session.Profile.Fingerprint, new(session.Profile.Generation.Value + 1)) }, expected.RunRevision, expected.JournalRevision)
        };
        var original = await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token);
        foreach (var mismatch in references) {
            await Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(() => scenario.Store.AcquirePlanningAuthoritySnapshotAsync(mismatch, scenario.Token));
        }
        var foreign = new FileSandboxWorkspaceStore(scenario.Files.WorkspaceRoot,
            WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N")));
        await Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(() => foreign.AcquirePlanningAuthoritySnapshotAsync(expected, scenario.Token));
        Assert.Equal(original, await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token));
        await scenario.RequireAllKeysAvailableAsync();
    });

    [Fact]
    public Task Cancellation_after_partial_acquisition_releases_selected_keys_without_publishing() => RunAsync(async scenario => {
        await scenario.HoldRunKeyAsync();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(scenario.Token);
        using var wait = new KeyWait();
        scenario.StartAcquire(cancelled.Token);
        await wait.Entered.Task.WaitAsync(scenario.Token);
        await scenario.RequireAgentKeyHeldAsync();
        await scenario.RequireWorkspaceAvailableAsync();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(scenario.ObserveAcquireAsync);
        Assert.Null(scenario.Lease);
        await scenario.ReleaseBlockerAsync();
        await scenario.RequireAllKeysAvailableAsync();
        AssertOriginalRun(scenario.Run, (await scenario.Store.GetExecutionRunAsync(scenario.Run.Id, scenario.Token))!);
    });

    [Fact]
    public Task Shared_pending_publication_during_key_wait_refuses_and_preserves_original_evidence() => RunAsync(async scenario => {
        await scenario.HoldRunKeyAsync();
        using var wait = new KeyWait();
        scenario.StartAcquire(scenario.Token);
        await wait.Entered.Task.WaitAsync(scenario.Token);
        await scenario.RequireAgentKeyHeldAsync();
        var runBytes = await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token);
        var catalogBytes = await File.ReadAllBytesAsync(scenario.Layout.CatalogPath, scenario.Token);
        var pending = Path.Combine(scenario.Layout.DataRoot, $".{Path.GetFileName(scenario.Layout.WorkspaceIndexPath)}.{Guid.NewGuid():N}.pending");
        var evidence = "retained incomplete publication"u8.ToArray();
        await using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            await output.WriteAsync(evidence, scenario.Token);
        }
        await scenario.ReleaseBlockerAsync();
        var failure = await Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(scenario.ObserveAcquireAsync);
        Assert.Equal(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged, failure.Kind);
        Assert.Null(scenario.Lease);
        Assert.Equal(evidence, await File.ReadAllBytesAsync(pending, scenario.Token));
        Assert.Equal(runBytes, await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token));
        Assert.Equal(catalogBytes, await File.ReadAllBytesAsync(scenario.Layout.CatalogPath, scenario.Token));
        await scenario.RequireAllKeysAvailableAsync();
    });

    private static void AssertOriginalRun(ExecutionRunRecord expected, ExecutionRunRecord actual) {
        var options = new FileSandboxWorkspaceJsonStore().SerializerOptions;
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(expected, options), JsonSerializer.SerializeToUtf8Bytes(actual, options));
    }

    private static async Task RunAsync(Func<Scenario, Task> body) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var failures = new List<Exception>();
        Scenario? scenario = null;
        try {
            scenario = await Scenario.CreateAsync(deadline.Token);
            await body(scenario);
        } catch (Exception failure) {
            failures.Add(failure);
        } finally {
            try {
                await deadline.CancelAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            if (scenario is not null) {
                await scenario.CleanupAsync(failures);
            }
        }
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1) {
            throw new AggregateException("Selected publication snapshot proof and cleanup failed.", failures);
        }
    }

    private sealed class Scenario {
        private readonly AgentToolAdmissionJournalFixture files;
        private readonly Guid unrelatedId;
        private readonly CancellationToken token;
        private readonly DurableFileWriter writer = new(new PhysicalFileSystemPathPolicyFactory());
        private readonly List<Task> pending = [];
        private readonly HashSet<Task> observed = [];
        private IAsyncDisposable? blocker;
        private Task<IAgentPlanningPublicationSnapshotLease>? acquiring;
        private bool acquisitionObserved;
        private bool leaseReleaseAttempted;
        internal AgentToolAdmissionJournalFixture Files => files;
        internal CancellationToken Token => token;
        internal FileSandboxWorkspaceStore Store => files.NewStore();
        internal FileSandboxWorkspaceStorageLayout Layout { get; }
        internal AgentDefinition Agent => files.Agent;
        internal Guid UnrelatedId => unrelatedId;
        internal ExecutionRunRecord Run => files.Detail.Run;
        internal AgentPlanningPublicationExpectation Expected => new(files.StorageScope, Run.ToolAdmission!.Session, Run.Revision, Run.ToolAdmission.Revision);
        internal IAgentPlanningPublicationSnapshotLease? Lease { get; private set; }
        private string[] KeyPaths => FileSandboxWorkspaceWriteSet.Run(Run, Run, true).LockPaths(Layout).ToArray();

        private Scenario(AgentToolAdmissionJournalFixture files, Guid unrelatedId, CancellationToken token) {
            this.files = files;
            this.unrelatedId = unrelatedId;
            this.token = token;
            Layout = new(files.WorkspaceRoot, files.StorageScope);
        }

        internal static async Task<Scenario> CreateAsync(CancellationToken token) {
            var profile = new AgentToolProfileBinding(Guid.NewGuid(), "planning-authority-fixture", new(1));
            var files = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: profile,
                storageScope: WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N")), configureAgent: agent => agent with {
                    Id = Guid.NewGuid(), Name = "Selected publication fixture", TemplateKey = "selected-publication-fixture",
                    IsTemplate = false, Status = AgentLifecycleStatus.Active, Permissions = agent.Permissions with { CanUseTools = true }
                });
            try {
                var unrelated = files.Agent with { Id = Guid.NewGuid(), Name = "Unrelated publication fixture",
                    TemplateKey = "unrelated-publication-fixture" };
                await files.Store.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, unrelated] }, token);
                return new(files, unrelated.Id, token);
            } catch (Exception primary) {
                try {
                    await files.DisposeAsync();
                } catch (Exception cleanup) {
                    throw new AggregateException(primary, cleanup);
                }
                throw;
            }
        }

        internal void StartAcquire(CancellationToken cancellationToken) {
            Assert.Null(acquiring);
            acquiring = Store.AcquirePlanningAuthoritySnapshotAsync(Expected, cancellationToken);
        }

        internal async Task<IAgentPlanningPublicationSnapshotLease> AcquireAsync() {
            StartAcquire(Token);
            await ObserveAcquireAsync();
            return Lease!;
        }

        internal async Task ObserveAcquireAsync() {
            try {
                Lease = await acquiring!;
            } finally {
                acquisitionObserved = true;
            }
        }

        internal Task<T> Track<T>(Task<T> task) {
            pending.Add(task);
            return task;
        }

        internal async Task ObserveAsync(Task task) {
            try {
                await task;
            } finally {
                observed.Add(task);
            }
        }

        internal async Task HoldRunKeyAsync() => blocker = await writer.AcquireCoordinationAsync(Layout.RootPath, KeyPaths[1],
            TimeSpan.FromSeconds(5), false, Token);

        internal async Task ReleaseBlockerAsync() {
            var released = blocker;
            blocker = null;
            if (released is not null) {
                await released.DisposeAsync();
            }
        }

        internal async Task ReleaseLeaseAsync() {
            var released = Lease;
            Lease = null;
            if (released is not null) {
                leaseReleaseAttempted = true;
                await released.DisposeAsync();
            }
        }

        internal async Task RequireAgentKeyHeldAsync() {
            var path = KeyPaths[0];
            while (true) {
                Token.ThrowIfCancellationRequested();
                if (File.Exists(path)) {
                    try {
                        using var available = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    } catch (IOException) {
                        Assert.False(acquiring!.IsCompleted);
                        return;
                    }
                }
                await Task.Delay(TimeSpan.FromMilliseconds(10), Token);
            }
        }

        internal async Task RequireWorkspaceAvailableAsync() {
            await using var available = await writer.AcquireCoordinationAsync(Layout.RootPath, Layout.WorkspaceLockPath,
                TimeSpan.FromSeconds(5), false, Token);
            Assert.NotNull(available);
        }

        internal async Task RequireAllKeysAvailableAsync() {
            foreach (var path in KeyPaths.Append(Layout.WorkspaceLockPath)) {
                await using var available = await writer.AcquireCoordinationAsync(Layout.RootPath, path, TimeSpan.FromSeconds(5), false, Token);
                Assert.NotNull(available);
            }
        }

        internal void RequireAllSelectedKeysHeld() {
            foreach (var path in KeyPaths) {
                Assert.True(File.Exists(path));
                Assert.Throws<IOException>(() => {
                    using var conflicting = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                });
            }
        }

        internal async Task CleanupAsync(List<Exception> failures) {
            await AttemptAsync(ReleaseLeaseAsync);
            await AttemptAsync(ReleaseBlockerAsync);
            if (acquiring is not null) {
                try {
                    var acquired = await acquiring;
                    if (!leaseReleaseAttempted) {
                        Lease ??= acquired;
                    }
                } catch (OperationCanceledException) when (Token.IsCancellationRequested) {
                } catch (Exception failure) {
                    if (!acquisitionObserved && !failures.Contains(failure)) {
                        failures.Add(failure);
                    }
                }
                await AttemptAsync(ReleaseLeaseAsync);
            }
            foreach (var task in pending) {
                try {
                    await task;
                } catch (OperationCanceledException) when (Token.IsCancellationRequested) {
                } catch (Exception failure) {
                    if (!observed.Contains(task) && !failures.Contains(failure)) {
                        failures.Add(failure);
                    }
                }
            }
            await AttemptAsync(() => files.DisposeAsync().AsTask());

            async Task AttemptAsync(Func<Task> cleanup) {
                try {
                    await cleanup();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
        }
    }

    private sealed class KeyWait : IDisposable {
        private readonly Activity parent = new Activity("selected-publication-test").Start();
        private readonly ActivityListener listener;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal KeyWait() {
            listener = new ActivityListener {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId == parent.TraceId && activity.OperationName == "workspace.publication.keys.wait") {
                        Entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public void Dispose() {
            listener.Dispose();
            parent.Dispose();
        }
    }
}
