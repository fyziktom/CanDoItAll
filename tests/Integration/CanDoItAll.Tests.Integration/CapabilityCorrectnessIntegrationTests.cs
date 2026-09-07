using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class CapabilityCorrectnessIntegrationTests {
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Proof_observation_time_does_not_regress_agent_revision_or_assignment_recovery(int observationOffsetDays) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var before = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        var assignment = new AgentCapabilityAssignmentAttempt(before, fixture.Capability.Id);
        var observed = before.ExpectedUpdatedAtUtc!.Value.AddDays(observationOffsetDays);
        fixture.Proof.CheckedAtUtc = observed;
        fixture.Proof.Release.SetResult();
        var result = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        Assert.Equal(CapabilityVerificationDisposition.Committed, result.Disposition);
        var canonical = await fixture.Store.LoadCatalogAsync();
        var agent = canonical.Agents.Single(item => item.Id == fixture.Agent.Id);
        Assert.True(agent.UpdatedAtUtc > before.ExpectedUpdatedAtUtc);
        Assert.Equal(observed, agent.Capabilities.Single().LastVerifiedAtUtc);
        Assert.Equal(observed, canonical.Capabilities.Single(item => item.Id == fixture.Capability.Id).LastVerifiedAtUtc);
        Assert.Equal(CapabilityProofRecovery.Satisfied, result.Receipt!.Classify(canonical.Agents, canonical.Capabilities));
        Assert.Equal(AgentCapabilityOperationStatus.Superseded, assignment.Classify(AgentEditorModel.FromDefinition(agent)));
        await Assert.ThrowsAsync<AgentCatalogConcurrencyException>(() => fixture.Catalog.SaveAgentAsync(assignment.CreateRequest()));
        Assert.Equal(agent.UpdatedAtUtc, (await fixture.Catalog.GetAgentEditorAsync(agent.Id)).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Future_agent_revision_survives_real_assignment_commit_and_read_only_recovery() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var future = DateTimeOffset.UtcNow.AddYears(2);
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id ? agent with { UpdatedAtUtc = future } : agent).ToArray()
        });
        var attempt = new AgentCapabilityAssignmentAttempt(await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id), fixture.Capability.Id);
        await fixture.Catalog.SaveAgentAsync(attempt.CreateRequest());
        var canonical = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        Assert.True(canonical.ExpectedUpdatedAtUtc > future);
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, attempt.Classify(canonical));
        Assert.Equal(1, fixture.StoreProbe.Writes);
    }

    [Fact]
    public async Task Final_provider_capture_receives_current_catalog_content_and_its_current_revision() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var provider = (await fixture.Store.LoadCatalogAsync()).Providers.First();
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id ? agent with { ProviderProfileId = provider.Id } : agent).ToArray()
        });
        var snapshots = new RevisionObservingProviders { FixedProfile = provider };
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, CapabilityStoreProbe>();
        var callbackStore = (CapabilityStoreProbe)(object)store;
        callbackStore.Inner = fixture.Store;
        callbackStore.BeforeUpdate = catalog => catalog with { CatalogDataRevision = catalog.CatalogDataRevision.Next() };
        var operation = new CapabilityVerificationPublication(store, fixture.Proof, snapshots);
        var pending = operation.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        await fixture.Proof.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Proof.Release.SetResult();
        var outcome = await pending;
        Assert.All(snapshots.Seen, pair => Assert.Equal(pair.Content, pair.Supplied));
        Assert.True(snapshots.Seen.Last().Content.Value > snapshots.Seen.First().Content.Value);
        Assert.Equal(CapabilityVerificationDisposition.Committed, outcome.Disposition);
    }

    [Fact]
    public async Task Unchanged_provider_lease_preserves_fingerprint_and_allows_proof_publication() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var provider = (await fixture.Store.LoadCatalogAsync()).Providers.First();
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id ? agent with { ProviderProfileId = provider.Id } : agent).ToArray()
        });
        fixture.Proof.Release.SetResult();
        var result = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        Assert.Equal(CapabilityVerificationDisposition.Committed, result.Disposition);
        Assert.Equal(provider.Id, fixture.Proof.CapturedProvider!.Id);
        Assert.Equal(1, fixture.Proof.Calls);
        Assert.Equal(1, fixture.StoreProbe.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initial_infrastructure_failure_is_not_input_rejection_and_never_dispatches_diagnostic(bool providerFailure) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        CapabilityVerificationOutcome outcome;
        if (providerFailure) {
            var provider = (await fixture.Store.LoadCatalogAsync()).Providers.First();
            await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent with { ProviderProfileId = provider.Id }).ToArray()
            });
            outcome = await new CapabilityVerificationPublication(fixture.Store, fixture.Proof, new RevisionObservingProviders { Unavailable = true })
                .ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        } else {
            fixture.StoreProbe.CatalogReadUnavailable = true;
            outcome = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        }
        Assert.Equal("InfrastructureUnavailable", outcome.Disposition.ToString());
        Assert.Equal(0, fixture.Proof.Calls);
        Assert.Equal(0, fixture.StoreProbe.Writes);
        Assert.Null(outcome.Receipt);
    }

    [Fact]
    public async Task Capability_deletion_advances_only_affected_agent_revisions_and_satisfies_attachment_recovery() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var draft = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        var attempt = new AgentCapabilityAssignmentAttempt(draft, fixture.Capability.Id);
        var before = await fixture.Store.LoadCatalogAsync();
        await fixture.Catalog.DeleteCapabilityAsync(fixture.Capability.Id);
        var canonical = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        Assert.True(canonical.ExpectedUpdatedAtUtc > draft.ExpectedUpdatedAtUtc);
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, attempt.Classify(canonical));
        var after = await fixture.Store.LoadCatalogAsync();
        foreach (var unaffected in before.Agents.Where(agent => agent.Id != fixture.Agent.Id && agent.Capabilities.All(item => item.CapabilityId != fixture.Capability.Id))) {
            Assert.Equal(unaffected.UpdatedAtUtc, after.Agents.Single(agent => agent.Id == unaffected.Id).UpdatedAtUtc);
        }
    }

    [Fact]
    public async Task Canonical_attachment_cleanup_advances_revision_once_and_recovery_observes_the_postcondition() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var orphan = Guid.NewGuid();
        var future = DateTimeOffset.UtcNow.AddYears(2);
        var before = (await fixture.Store.LoadCatalogAsync()).Agents.Single(agent => agent.Id == fixture.Agent.Id) with {
            UpdatedAtUtc = future,
            Capabilities = [new(fixture.Capability.Id, fixture.Capability.Key, fixture.Capability.Kind, CapabilityProofStatus.NotRun, null, ""),
                new(orphan, "orphan-fixture", CapabilityKind.Skill, CapabilityProofStatus.NotRun, null, "")]
        };
        var attempt = new AgentCapabilityAssignmentAttempt(AgentEditorModel.FromDefinition(before), orphan);
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == before.Id ? before : agent).ToArray()
        });
        var canonical = await fixture.Catalog.GetAgentEditorAsync(before.Id);
        Assert.DoesNotContain(orphan, canonical.SelectedCapabilityIds);
        Assert.True(canonical.ExpectedUpdatedAtUtc > future);
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, attempt.Classify(canonical));
        Assert.Equal(canonical.ExpectedUpdatedAtUtc, (await fixture.Catalog.GetAgentEditorAsync(before.Id)).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Missing_snapshot_source_is_infrastructure_unavailable_before_diagnostic_dispatch() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var profiles = new ProviderProfileService();
        var registry = new WorkspaceBackedProviderProfileRegistry(fixture.Store, profiles);
        var catalog = new AgentFrameworkWorkspaceCatalogService(fixture.Store,
            DispatchProxy.Create<IAgentPackageService, UnusedCapabilityDependency>(), fixture.Proof, profiles,
            DispatchProxy.Create<IProviderDiagnosticsService, UnusedCapabilityDependency>(), registry,
            DispatchProxy.Create<IProviderRuntimeProfileSource, UnusedCapabilityDependency>());
        var failure = await Assert.ThrowsAsync<CapabilityVerificationException>(() => catalog.VerifyCapabilityAsync(fixture.Agent.Id, fixture.Capability.Id));
        Assert.Equal(CapabilityVerificationDisposition.InfrastructureUnavailable, failure.Outcome.Disposition);
        Assert.Equal(0, fixture.Proof.Calls);
        Assert.Equal(0, fixture.StoreProbe.Writes);
    }

    [Fact]
    public async Task Unavailable_verification_API_returns_sanitized_conflict_without_automatic_replay() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        fixture.StoreProbe.CatalogReadUnavailable = true;
        var outcome = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CapabilityApiWorkspace>();
        ((CapabilityApiWorkspace)(object)workspace).Outcome = outcome;
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, configureServices: services => services.Replace(ServiceDescriptor.Singleton(workspace)));
        using var response = await host.Client.PostAsJsonAsync($"/api/agents/{fixture.Agent.Id:D}/capabilities/{fixture.Capability.Id:D}/verify", new { });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("InfrastructureUnavailable", json.RootElement.GetProperty("outcome").GetString());
        Assert.False(json.RootElement.GetProperty("automaticReplaySafe").GetBoolean());
        Assert.Equal(fixture.Agent.Id, json.RootElement.GetProperty("agentId").GetGuid());
        Assert.DoesNotContain("IOException", body);
        Assert.DoesNotContain("Catalog fixture", body);
        Assert.DoesNotContain(fixture.RootPath, body);
    }

    private sealed class RevisionObservingProviders : IProviderRuntimeProfileSnapshotSource {
        public List<(CatalogDataRevision Content, CatalogDataRevision Supplied)> Seen { get; } = [];
        public bool Unavailable { get; init; }
        public ProviderProfile? FixedProfile { get; init; }
        public ProviderRuntimeProfileSnapshotLease? CaptureProvider(Guid providerId, SandboxWorkspaceCatalogSnapshot snapshot) {
            if (Unavailable) {
                throw new IOException("Private provider snapshot is unavailable");
            }
            Seen.Add((snapshot.Catalog.CatalogDataRevision, snapshot.Revision));
            var provider = FixedProfile ?? snapshot.Catalog.Providers.Single(item => item.Id == providerId);
            return new(provider, ProviderConfigurationFingerprintFactory.Create(provider));
        }
    }
}
