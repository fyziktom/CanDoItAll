using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class CapabilityProofPublicationIntegrationTests {
    [Fact]
    public async Task Registered_database_runtime_revision_change_supersedes_inline_proof() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(new() {
            Name = "Proof revision fixture", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = "proof-model", SuggestedModels = ["proof-model"], IsEnabled = true
        });
        var source = Assert.IsAssignableFrom<IProviderRuntimeProfileSnapshotSource>(scope.ServiceProvider.GetRequiredService<IProviderRuntimeProfileSource>());
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var provider = Assert.IsType<ProviderProfile>(await registry.GetProviderAsync(id));
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Providers = [.. catalog.Providers, provider],
            Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id ? agent with { ProviderProfileId = id } : agent).ToArray()
        });
        var operation = new CapabilityVerificationPublication(fixture.Store, fixture.Proof, source);
        var pending = operation.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        await fixture.Proof.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(id, fixture.Proof.CapturedProvider!.Id);
        var editor = await registry.GetProviderEditorAsync(id);
        var previous = editor.ExpectedConcurrencyToken;
        editor.IsEnabled = false;
        await registry.SaveProviderAsync(editor);
        Assert.NotEqual(previous, (await registry.GetProviderEditorAsync(id)).ExpectedConcurrencyToken);
        fixture.Proof.Release.SetResult();
        var result = await pending;
        Assert.Equal(CapabilityVerificationDisposition.Superseded, result.Disposition);
        var canonical = await fixture.Store.LoadCatalogAsync();
        Assert.Equal(fixture.Agent.UpdatedAtUtc, canonical.Agents.Single(agent => agent.Id == fixture.Agent.Id).UpdatedAtUtc);
        Assert.Equal(CapabilityProofStatus.NotRun, canonical.Capabilities.Single(capability => capability.Id == fixture.Capability.Id).ProofStatus);
        Assert.Equal(1, fixture.Proof.Calls);
    }

    [Fact]
    public async Task Successful_diagnostic_persists_agent_and_catalog_proof_once() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        fixture.Proof.Release.SetResult();
        var result = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        Assert.Equal(CapabilityVerificationDisposition.Committed, result.Disposition);
        var receipt = Assert.IsType<CapabilityProofReceipt>(result.Receipt);
        var canonical = await fixture.Store.LoadCatalogAsync();
        Assert.Equal(CapabilityProofRecovery.Satisfied, receipt.Classify(canonical.Agents, canonical.Capabilities));
        Assert.Equal(fixture.Agent.Id, receipt.AgentId);
        Assert.Equal(fixture.Capability.Id, receipt.CapabilityId);
        Assert.Equal(fixture.Agent.UpdatedAtUtc, receipt.ExpectedUpdatedAtUtc);
        Assert.Equal(64, receipt.InputFingerprint.Length);
        Assert.Equal(CapabilityProofStatus.Verified, canonical.Agents.Single(agent => agent.Id == fixture.Agent.Id).Capabilities.Single().ProofStatus);
        Assert.Equal(1, fixture.Proof.Calls);
        Assert.Equal(1, fixture.StoreProbe.Writes);
    }

    [Fact]
    public async Task Proof_commit_then_index_failure_is_canonically_verified_without_diagnostic_replay() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        fixture.Proof.Release.SetResult();
        var index = Path.Combine(WorkspaceScopeDescriptor.Sandbox.ResolveDataRoot(fixture.RootPath), "workspace.index.json");
        var preserved = index + ".preserved";
        fixture.StoreProbe.BeforeCatalogWrite = () => {
            File.Move(index, preserved);
            Directory.CreateDirectory(index);
        };
        var outcome = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        Assert.Equal(CapabilityVerificationDisposition.Unconfirmed, outcome.Disposition);
        fixture.StoreProbe.BeforeCatalogWrite = null;
        Directory.Delete(index);
        File.Move(preserved, index);
        var canonical = await fixture.Store.LoadCatalogAsync();
        var receipt = Assert.IsType<CapabilityProofReceipt>(outcome.Receipt);
        Assert.Equal(CapabilityProofRecovery.Satisfied, receipt.Classify(canonical.Agents, canonical.Capabilities));
        Assert.Equal(CapabilityProofRecovery.Satisfied, receipt.Classify(canonical.Agents, canonical.Capabilities));
        Assert.Equal(1, fixture.Proof.Calls);
        Assert.Equal(1, fixture.StoreProbe.Writes);
    }

    [Fact]
    public async Task Changed_agent_permissions_supersede_diagnostic_without_advancing_timestamp() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var pending = fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        await fixture.Proof.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent with { Permissions = agent.Permissions with { CanUseTools = !agent.Permissions.CanUseTools } }).ToArray()
        });
        fixture.Proof.Release.SetResult();
        Assert.Equal(CapabilityVerificationDisposition.Superseded, (await pending).Disposition);
        Assert.Equal(fixture.Agent.UpdatedAtUtc, (await fixture.Store.LoadCatalogAsync()).Agents.Single(agent => agent.Id == fixture.Agent.Id).UpdatedAtUtc);
    }

    [Fact]
    public async Task Changed_provider_inputs_supersede_captured_diagnostic() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var catalog = await fixture.Store.LoadCatalogAsync();
        var provider = catalog.Providers.First();
        await fixture.Store.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => agent with { ProviderProfileId = provider.Id }).ToArray()
        });
        var pending = fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        await fixture.Proof.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(provider.Id, fixture.Proof.CapturedProvider?.Id);
        await fixture.Store.UpdateCatalogAsync(current => current with {
            Providers = current.Providers.Select(item => item.Id == provider.Id ? item with { SupportsTools = !item.SupportsTools } : item).ToArray()
        });
        fixture.Proof.Release.SetResult();
        Assert.Equal(CapabilityVerificationDisposition.Superseded, (await pending).Disposition);
        Assert.Equal(0, fixture.StoreProbe.Writes);
        Assert.Equal(1, fixture.Proof.Calls);
    }

    [Fact]
    public async Task Input_change_inside_locked_write_callback_supersedes_diagnostic() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        fixture.Proof.Release.SetResult();
        fixture.Proof.AfterDiagnostic = () => fixture.StoreProbe.BeforeUpdate = catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent with { Capabilities = [] }).ToArray()
        };
        var result = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, CancellationToken.None);
        Assert.Equal(CapabilityVerificationDisposition.Superseded, result.Disposition);
        Assert.Equal(fixture.Agent.UpdatedAtUtc, (await fixture.Store.LoadCatalogAsync()).Agents.Single(agent => agent.Id == fixture.Agent.Id).UpdatedAtUtc);
    }

    [Fact]
    public async Task Owner_cancellation_after_diagnostic_before_publication_does_not_write_proof() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        using var owner = new CancellationTokenSource();
        fixture.Proof.Release.SetResult();
        fixture.Proof.AfterDiagnostic = owner.Cancel;
        var outcome = await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, owner.Token);
        Assert.Equal(CapabilityVerificationDisposition.PublicationCanceled, outcome.Disposition);
        var canonical = await fixture.Store.LoadCatalogAsync();
        Assert.Equal(CapabilityProofRecovery.NotPublished, outcome.Receipt!.Classify(canonical.Agents, canonical.Capabilities));
        Assert.Equal(0, fixture.StoreProbe.Writes);
        Assert.Equal(1, fixture.Proof.Calls);
    }

    [Fact]
    public async Task Unattached_or_precanceled_diagnostic_performs_no_observation_or_write() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        using var owner = new CancellationTokenSource();
        owner.Cancel();
        Assert.Equal(CapabilityVerificationDisposition.CanceledBeforeDiagnostic,
            (await fixture.Verification.ExecuteAsync(fixture.Agent.Id, fixture.Capability.Id, owner.Token)).Disposition);
        Assert.Equal(CapabilityVerificationDisposition.Rejected,
            (await fixture.Verification.ExecuteAsync(fixture.Agent.Id, Guid.NewGuid(), CancellationToken.None)).Disposition);
        Assert.Equal(0, fixture.Proof.Calls);
        Assert.Equal(0, fixture.StoreProbe.Writes);
    }
}
