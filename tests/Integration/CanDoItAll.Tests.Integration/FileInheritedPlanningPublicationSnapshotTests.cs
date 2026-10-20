using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;
using static CanDoItAll.Tests.Integration.Runtime.FileAgentPlanningPublicationSnapshotTests;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class FileInheritedPlanningPublicationSnapshotTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Original_inherited_run_has_no_chat_and_holds_only_selected_catalog_evidence(bool additional)
        => RunAsync(async scenario => {
            var lease = await scenario.AcquireAsync();
            Assert.Equal(AgentPlanningPublicationOrigin.Inherited, lease.Original.Origin);
            Assert.Null(lease.OriginalChatSession);
            Assert.Null(lease.OriginalRun.ChatSessionId);
            Assert.Equal(scenario.Run.Id, lease.OriginalRun.Id);
            Assert.Equal(scenario.Run.MetadataJson, lease.OriginalRun.MetadataJson);
            Assert.Equal(scenario.Run.ToolAdmission!.PlanningDecision!.InheritedOrigin,
                lease.OriginalRun.ToolAdmission!.PlanningDecision!.InheritedOrigin);
            Assert.Equal(scenario.Run.ToolAdmission.Session, lease.Original.Session);
            Assert.Equal(scenario.Agent.Id, lease.Agent.Id);
            Assert.Equal(additional ? 2 : 1, lease.Agents.Length);
            Assert.Equal(additional ? 3 : 2, scenario.KeyPaths.Length);
            Assert.Throws<NotSupportedException>(() => ((IList<AgentDefinition>)lease.Agents).Clear());
            scenario.RequireAllSelectedKeysHeld();
            await scenario.RequireWorkspaceAvailableAsync();
            await scenario.ReleaseLeaseAsync();
            Assert.Throws<ObjectDisposedException>(() => lease.OriginalChatSession);
            await scenario.RequireAllKeysAvailableAsync();
        }, inherited: true, additionalAgent: additional);

    [Fact]
    public Task Both_selected_Agents_have_current_frozen_capabilities_and_revocation_state()
        => RunAsync(async scenario => {
            var first = Capability("executor-snapshot");
            var second = Capability("source-snapshot");
            var unrelated = Capability("unrelated-snapshot");
            await scenario.Store.UpdateCatalogAsync(catalog => catalog with {
                Capabilities = [.. catalog.Capabilities, first, second, unrelated],
                Agents = catalog.Agents.Select(agent => agent.Id == scenario.Agent.Id ? Changed(agent, first) :
                    agent.Id == scenario.AdditionalAgentId ? Changed(agent, second) : agent).ToArray()
            }, scenario.Token);
            var lease = await scenario.AcquireAsync();
            Assert.Equal(new[] { first.Id, second.Id }.Order(), lease.Capabilities.Select(item => item.Id).Order());
            Assert.All(lease.Agents, agent => {
                Assert.False(agent.Permissions.CanUseTools);
                Assert.Equal(AgentLifecycleStatus.Suspended, agent.Status);
                Assert.Throws<NotSupportedException>(() => ((IList<AgentCapabilityAssignment>)agent.Capabilities).Clear());
            });
            Assert.All(lease.Capabilities, item => Assert.Throws<NotSupportedException>(() => ((IList<string>)item.Tags).Clear()));
            Assert.Equal(scenario.Run.ToolAdmission!.PlanningDecision!.InheritedOrigin,
                lease.OriginalRun.ToolAdmission!.PlanningDecision!.InheritedOrigin);
        }, inherited: true, additionalAgent: true);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Either_selected_Agent_writer_waits_and_an_unrelated_publication_is_never_overwritten(bool source, bool unrelatedChanges)
        => RunAsync(async scenario => {
            var selected = source ? scenario.AdditionalAgentId!.Value : scenario.Agent.Id;
            var lease = await scenario.AcquireAsync();
            using var wait = new KeyWait();
            SandboxWorkspaceCatalog Revoke(SandboxWorkspaceCatalog catalog) => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == selected ? agent with {
                    Permissions = agent.Permissions with { CanUseTools = false }
                } : agent).ToArray()
            };
            var change = scenario.Track(scenario.Store.UpdateCatalogAsync(Revoke, scenario.Token));
            await wait.Entered.Task.WaitAsync(scenario.Token);
            Assert.False(change.IsCompleted);
            await scenario.RequireWorkspaceAvailableAsync();
            if (unrelatedChanges) {
                await scenario.Store.UpdateCatalogAsync(catalog => catalog with {
                    Agents = catalog.Agents.Select(agent => agent.Id == scenario.UnrelatedId
                        ? agent with { Summary = "Third Agent progressed" } : agent).ToArray()
                }, scenario.Token);
            }
            Assert.True(lease.Agents.Single(agent => agent.Id == selected).Permissions.CanUseTools);
            await scenario.ReleaseLeaseAsync();
            if (unrelatedChanges) {
                var conflict = await Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(() => scenario.ObserveAsync(change));
                Assert.Equal(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged, conflict.Kind);
                await scenario.Store.UpdateCatalogAsync(Revoke, scenario.Token);
            } else {
                await scenario.ObserveAsync(change);
            }
            var current = await scenario.Store.LoadCatalogAsync(scenario.Token);
            Assert.False(current.Agents.Single(agent => agent.Id == selected).Permissions.CanUseTools);
            if (unrelatedChanges) {
                Assert.Equal("Third Agent progressed", current.Agents.Single(agent => agent.Id == scenario.UnrelatedId).Summary);
            }
            await scenario.RequireAllKeysAvailableAsync();
        }, inherited: true, additionalAgent: true);

    [Theory]
    [InlineData(Mismatch.RunRevision)]
    [InlineData(Mismatch.JournalRevision)]
    [InlineData(Mismatch.Agent)]
    [InlineData(Mismatch.ProfileFingerprint)]
    [InlineData(Mismatch.ProfileGeneration)]
    [InlineData(Mismatch.SourceId)]
    [InlineData(Mismatch.SourceFingerprint)]
    [InlineData(Mismatch.MissingAdditionalAgent)]
    public Task Original_identity_substitution_or_missing_selected_Agent_refuses_and_releases_every_key(Mismatch mismatch)
        => RunAsync(async scenario => {
            var session = scenario.Expected.Session;
            session = mismatch switch {
                Mismatch.Agent => session with { AgentId = Guid.NewGuid() },
                Mismatch.ProfileFingerprint => session with { Profile = new(session.Profile.ProfileId, "replaced", session.Profile.Generation) },
                Mismatch.ProfileGeneration => session with { Profile = new(session.Profile.ProfileId, session.Profile.Fingerprint,
                    new(session.Profile.Generation.Value + 1)) },
                _ => session
            };
            if (mismatch is Mismatch.SourceId or Mismatch.SourceFingerprint) {
                var source = session.Reference.BackgroundSource!;
                session = session with { Reference = new(session.Reference.ExecutionRunId, Guid.Empty, default,
                    new(source.SourceKind, mismatch == Mismatch.SourceId ? "replaced-source" : source.SourceId,
                        mismatch == Mismatch.SourceFingerprint ? AgentPlanningDecisionTestData.Envelope("{\"changed\":true}").Digest : source.OwnerFingerprint,
                        source.ExecutionFingerprint)) };
            }
            var expected = AgentPlanningPublicationExpectation.Inherited(scenario.Files.StorageScope, session,
                scenario.Run.Revision + (mismatch == Mismatch.RunRevision ? 1 : 0),
                scenario.Run.ToolAdmission!.Revision + (mismatch == Mismatch.JournalRevision ? 1 : 0),
                mismatch == Mismatch.MissingAdditionalAgent ? Guid.NewGuid() : scenario.AdditionalAgentId);
            await Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(() =>
                scenario.Store.AcquirePlanningAuthoritySnapshotAsync(expected, scenario.Token));
            await scenario.RequireAllKeysAvailableAsync();
            var valid = await scenario.AcquireAsync();
            Assert.Equal(scenario.Run.ToolAdmission.Session, valid.Original.Session);
        }, inherited: true, additionalAgent: true);

    [Fact]
    public Task Cancellation_after_both_Agent_keys_are_acquired_releases_the_whole_set()
        => RunAsync(async scenario => {
            await scenario.HoldRunKeyAsync();
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(scenario.Token);
            using var wait = new KeyWait();
            scenario.StartAcquire(cancelled.Token);
            await wait.Entered.Task.WaitAsync(scenario.Token);
            await scenario.RequireAgentKeyHeldAsync();
            await scenario.RequireWorkspaceAvailableAsync();
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(scenario.ObserveAcquireAsync);
            await scenario.ReleaseBlockerAsync();
            await scenario.RequireAllKeysAvailableAsync();
            Assert.Null(scenario.Lease);
        }, inherited: true, additionalAgent: true);

    [Fact]
    public Task Reversed_executor_and_source_acquire_one_consistent_order_and_finish_after_release()
        => RunAsync(async scenario => {
            var second = await CreateOtherRunAsync(scenario);
            var expected = AgentPlanningPublicationExpectation.Inherited(scenario.Files.StorageScope, second.ToolAdmission!.Session,
                second.Revision, second.ToolAdmission.Revision, scenario.Agent.Id);
            var otherKeys = FileSandboxWorkspaceWriteSet.Planning(second, scenario.Agent.Id).LockPaths(scenario.Layout);
            Assert.Equal(scenario.KeyPaths.Take(2), otherKeys.Take(2));
            await scenario.AcquireAsync();
            using var wait = new KeyWait();
            var other = scenario.Track(AcquireOtherAsync());
            await wait.Entered.Task.WaitAsync(scenario.Token);
            Assert.False(other.IsCompleted);
            await scenario.RequireWorkspaceAvailableAsync();
            await scenario.ReleaseLeaseAsync();
            await scenario.ObserveAsync(other);
            Assert.True(await other);
            await scenario.RequireAllKeysAvailableAsync();

            async Task<bool> AcquireOtherAsync() {
                await using var held = await scenario.Store.AcquirePlanningAuthoritySnapshotAsync(expected, scenario.Token);
                Assert.Equal(second.AgentId, held.Agent.Id);
                Assert.Equal(second.Id, held.OriginalRun.Id);
                Assert.Equal(2, held.Agents.Length);
                await scenario.RequireWorkspaceAvailableAsync();
                return true;
            }
        }, inherited: true, additionalAgent: true);

    [Fact]
    public Task Snapshot_does_not_promote_inherited_work_to_effect_dispatch()
        => RunAsync(async scenario => {
            var fixture = scenario.Inherited;
            await using var dispatch = await fixture.StartAsync();
            using var bound = dispatch.Bind();
            var work = Assert.IsType<AgentPlanningLiveWork>(dispatch.PlanningWork);
            await fixture.InheritAsync(work);
            var provider = await fixture.EnterAsync(work);
            var result = await fixture.Owner.AdmitResponseAsync(work, provider.Id, provider.RequestDigest,
                AgentPlanningDecisionTestData.Envelope("{\"response\":1}"), AgentPlanningLiveOwnerFixture.Publication(), scenario.Token);
            var run = result.Run;
            var binding = Assert.Single(run.ToolAdmission!.PlanningDecision!.Current.Steps[^1].Publications).Proposal;
            await using var held = await scenario.Store.AcquirePlanningAuthoritySnapshotAsync(
                AgentPlanningPublicationExpectation.Inherited(scenario.Files.StorageScope, run.ToolAdmission.Session,
                    run.Revision, run.ToolAdmission.Revision, scenario.AdditionalAgentId), scenario.Token);
            var refusal = Assert.Throws<AgentPlanningAdmissionException>(() => work.RequirePublication(held, binding));
            Assert.Equal(AgentPlanningAdmissionRefusal.OriginalStepChanged, refusal.Reason);
            Assert.Null(AgentToolInvocationClaim.Current);
        }, inherited: true, additionalAgent: true);

    private static CapabilityCatalogItem Capability(string key) => new(Guid.NewGuid(), CapabilityKind.Tool, key, key,
        "", "", "{}", CapabilityProofStatus.NotRun, "", null, false) { Tags = ["snapshot-fixture"] };

    private static AgentDefinition Changed(AgentDefinition agent, CapabilityCatalogItem capability) => agent with {
        Status = AgentLifecycleStatus.Suspended,
        Permissions = agent.Permissions with { CanUseTools = false },
        Capabilities = [new(capability.Id, capability.Key, capability.Kind, capability.ProofStatus, null, "")]
    };

    private static async Task<ExecutionRunRecord> CreateOtherRunAsync(Scenario scenario) {
        var agentId = scenario.AdditionalAgentId!.Value;
        var source = new AgentInheritedPlanningFixture.SourcePolicy(agentId, scenario.Files.Profile);
        var original = scenario.Run.ToolAdmission!.PlanningDecision!;
        var candidate = scenario.Run with { Id = Guid.NewGuid(), AgentId = agentId, ToolAdmission = null, SourceId = source.SourceId };
        var admission = new AgentInheritedPlanningAdmission(new(scenario.Store, scenario.Files.Profile,
            scenario.Inherited.Clock, backgroundSources: [source]));
        var journal = await admission.CreateAsync(candidate, "Read original inherited material.", original.Owner, original.InheritedOrigin!.ParentBinding,
            (binding, _) => Task.FromResult(new AgentInheritedPlanningBinding(original.OriginalTarget,
                original.InheritedOrigin! with { AgentId = agentId, Source = binding.Session.Reference.BackgroundSource! })), scenario.Token);
        return (await scenario.Store.SaveExecutionRunDetailAsync(new(candidate with { ToolAdmission = journal }, null, [], []), scenario.Token)).Run;
    }

    public enum Mismatch {
        RunRevision,
        JournalRevision,
        Agent,
        ProfileFingerprint,
        ProfileGeneration,
        SourceId,
        SourceFingerprint,
        MissingAdditionalAgent
    }
}
